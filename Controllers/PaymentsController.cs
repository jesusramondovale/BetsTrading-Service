namespace BetsTrading_Service.Controllers
{
  using BetsTrading_Service.Database;
  using BetsTrading_Service.Interfaces;
  using BetsTrading_Service.Requests;
  using Google.Apis.Auth.OAuth2.Requests;
  using Microsoft.AspNetCore.Identity.Data;
  using Microsoft.AspNetCore.Mvc;
  using Microsoft.EntityFrameworkCore;
  using Stripe;
  using Stripe.Forwarding;
  using System.IO;
  using System.Runtime.CompilerServices;
  using System.Security.Cryptography;
  using System.Text.Json;
  using System.Text.Json.Serialization;

  [ApiController]
  [Route("api/[controller]")]
  public class PaymentsController : ControllerBase
  {
    private readonly IConfiguration _config;
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;


    public PaymentsController(AppDbContext dbContext, IConfiguration config, ICustomLogger customLogger)
    {
      _dbContext = dbContext;
      _config = config;
      _logger = customLogger;
      StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY", EnvironmentVariableTarget.User) ?? "";

  }

    [HttpPost("CreatePaymentIntent")]
    public IActionResult CreatePaymentIntent([FromBody] CreatePaymentIntentRequest req)
    {
      string test = StripeConfiguration.ApiKey;

      try
      {
        // Añadimos userId en metadata
        var options = new PaymentIntentCreateOptions
        {
          Amount = req.Amount,
          Currency = req.Currency,
          AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
          {
            Enabled = true
          },
          
          Metadata = new Dictionary<string, string>
                {
                    { "userId", req.UserId! },
                    { "coins", req!.Coins.ToString() }
                }
        };

        var service = new PaymentIntentService();
        var intent = service.Create(options);

        return Ok(new { client_secret = intent.ClientSecret });
      }
      catch (Exception ex)
      {
        return StatusCode(500, new { message = "Stripe error", error = ex.Message });
      }
    }


    [HttpPost("Webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
      var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
      try
      {
        var sigHeader = Request.Headers["Stripe-Signature"];
        var endpointSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET", EnvironmentVariableTarget.User) ?? "";

        var stripeEvent = EventUtility.ConstructEvent(json, sigHeader, endpointSecret);

        if (stripeEvent.Type == "payment_intent.succeeded")
        {
          var intent = stripeEvent.Data.Object as PaymentIntent;
          var userId = intent!.Metadata["userId"];
          var coins = double.Parse(intent.Metadata["coins"], System.Globalization.CultureInfo.InvariantCulture);

          _logger.Log.Information($"[Stripe] Pago confirmado para {userId} con {coins} coins");

          var user = _dbContext.Users.FirstOrDefault(u => u.id == userId);
          if (user != null)
          {
            user.points += coins;
            await _dbContext.SaveChangesAsync();
          }
        }
        else if (stripeEvent.Type == "payment_intent.payment_failed")
        {
          _logger.Log.Warning("[Stripe] Pago fallido.");
        }

        return Ok();
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[ERROR] Stripe Webhook");
        return BadRequest();
      }
    }


    [HttpPost("RetireBalance")]
    public async Task<IActionResult> RetireBalance([FromBody] RetireBalanceRequest req)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users.FirstOrDefault(u => u.fcm == req.fcm && u.id == req.UserId);
          if (user != null)
          {
            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.password))
            {
              string? ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();
              var geo = await AuthController.GetGeoLocationFromIp(ip!);
              if (geo == null)
              {
                _logger.Log.Error("[PAYMENTS] :: INCORRECT RETIRE ATTEMPT FOR USER {user} FROM IP {ip}", req.UserId, ip ?? "UNKNOWN");
              }
              else
              {
                _logger.Log.Error("[PAYMENTS] :: INCORRECT RETIRE ATTEMPT OF {coins} coins FOR USER {user} FROM IP {ip} -> {city} ,{region} ,{country} , ISP: {isp}",
                    req.Coins, req.UserId, ip ?? "UNKNOWN", geo.City, geo.RegionName, geo.Country, geo.ISP);
              }
              return BadRequest(new { Message = "Incorrect password" });
            }
            var path = Path.Combine(Directory.GetCurrentDirectory(), $"exchange_options_{req.Currency}.json");
            var optionsJson = await System.IO.File.ReadAllTextAsync(path);
            var options = JsonSerializer.Deserialize<List<ExchangeOption>>(optionsJson);

            if (!options!.Any(o => o.Type == "exchange" && o.Coins == req.Coins && o.Euros == req.CurrencyAmount))
            {
              _logger.Log.Warning("[PAYMENTS] :: RetireBalance :: Invalid coins/amount combination: {coins} - {euros}", req.Coins, req.CurrencyAmount);
              return BadRequest(new { Message = "Invalid coins/amount combination." });
            }

            user.pending_balance += req.CurrencyAmount;
            user.points -= req.Coins;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.Log.Information("[PAYMENTS] :: RetireBalance :: Success with User ID {id}", req.UserId);
            return Ok(new { Message = $"Retired {req.CurrencyAmount}€ ({req.Coins} coins) of user {req.UserId} successfully" });
          }
          else
          {
            return NotFound(new { Message = "User not found or session expired" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[PAYMENTS] :: RetireBalance :: Internal Server Error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }

    public class ExchangeOption
    {
      [JsonPropertyName("coins")]
      public int Coins { get; set; }

      [JsonPropertyName("euros")]
      public double Euros { get; set; }

      [JsonPropertyName("type")]
      public string Type { get; set; } = string.Empty;
    }



    [HttpGet("VerifyAd"), HttpPost("VerifyAd")]
    public async Task<IActionResult> Verify(
     [FromQuery] string? reward_amount,
     [FromQuery] string? reward_type,
     [FromQuery] string? transaction_id,
     [FromQuery] string? custom_data,
     [FromQuery] string? key_id,
     [FromQuery] string? signature)
    {
      _logger.Log.Information("[Payments] :: VerifyAd: {q}", Request.QueryString);

      // Verificación inicial de URL (Google no manda firma ni key_id en ese caso)
      if (string.IsNullOrEmpty(signature) || string.IsNullOrEmpty(key_id))
        return Ok();

      try
      {
        // Construir mensaje
        var message = $"reward_amount={reward_amount}&reward_type={reward_type}&transaction_id={transaction_id}&user_id={custom_data}";


        // Descargar claves públicas de Google
        using var http = new HttpClient();
        var json = await http.GetStringAsync("https://www.gstatic.com/admob/reward/verifier-keys.json");
        var keys = System.Text.Json.JsonDocument.Parse(json).RootElement.GetProperty("keys");



        JsonElement? keyData = null;

        foreach (var k in keys.EnumerateArray())
        {
          if (k.ValueKind == JsonValueKind.Object &&
              k.TryGetProperty("keyId", out var kid))
          {
            // Solo trabajamos si el valor es string
            if (kid.ValueKind == JsonValueKind.String)
            {
              var val = kid.GetString();
              if (!string.IsNullOrEmpty(val) && val == key_id)
              {
                keyData = k;
                break;
              }
            }
          }
        }

        if (keyData == null || keyData.Value.ValueKind == JsonValueKind.Undefined)
        {
          _logger.Log.Warning("[Payments] :: KeyId {key_id} no encontrada en claves públicas de Google", key_id);
          // No reventamos: devolvemos 200 para no bloquear anuncios de prueba
          return Ok();
        }

        // Extraer n y e
        var nStr = keyData.Value.GetProperty("n").GetString();
        var eStr = keyData.Value.GetProperty("e").GetString();

        if (string.IsNullOrEmpty(nStr) || string.IsNullOrEmpty(eStr))
        {
          _logger.Log.Warning("[Payments] :: Clave pública incompleta para KeyId {key_id}", key_id);
          return Ok();
        }

        var n = Convert.FromBase64String(nStr);
        var e = Convert.FromBase64String(eStr);

        // Verificar firma
        using var rsa = RSA.Create();
        rsa.ImportParameters(new RSAParameters { Modulus = n, Exponent = e });

        var data = System.Text.Encoding.UTF8.GetBytes(message);
        var sig = Convert.FromBase64String(signature);

        bool valid = rsa.VerifyData(data, sig, HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        if (!valid)
          return Unauthorized();

        // Lógica de negocio
        using (var transaction = await _dbContext.Database.BeginTransactionAsync())
        {
          var user = _dbContext.Users.FirstOrDefault(u => u.id == custom_data);


          if (user != null)
          {
            user.points += double.Parse(reward_amount!);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.Log.Information("[INFO] :: AddCoins :: Success on user ID: {msg}", custom_data);
            return Ok(new { });

          }
          else
          {
            _logger.Log.Warning("[WARN] :: AddCoins :: User not found for ID: {msg}", custom_data);
            transaction.Rollback();
            return NotFound(new { Message = "User not found" });
          }

        }

      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[ERROR] :: AddCoins :: Exception en VerifyAd");
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

    }



    /** TODO: Delete AddCoins endpoint when using real ADMOB_TOKEN with SSV : 
     * All its business logic goes into -> PaymentsController:59 (HTTP GET VerifyAd) which
     * will be called by GoogleAdmob system automatically when user finishes watching real ads
     */
    [HttpPost("AddCoins")]
    public async Task<IActionResult> AddCoins([FromBody] addCoinsRequest coinsRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users
              .FirstOrDefault(u => u.id == coinsRequest.user_id);

          if (user != null) // User exists
          {
            user.points += coinsRequest.reward ?? 0;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.Log.Information("[INFO] :: AddCoins :: Success on user ID: {msg}", coinsRequest.user_id);
            return Ok(new { });

          }
          else // Unexistent user
          {
            _logger.Log.Warning("[WARN] :: AddCoins :: User not found for ID: {msg}", coinsRequest.user_id);
            return NotFound(new { Message = "User not found" }); // User not found
          }
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[ERROR] :: AddCoins :: Internal server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }

      }

    }


  }

  public class RetireBalanceRequest
  {
    public string? UserId { get; set; }
    public string? fcm{ get; set; }
    public string? Password { get; set; }
    public double CurrencyAmount { get; set; }
    public string? Currency { get; set; }
    public double Coins { get; set; }
    
  }

  public class CreatePaymentIntentRequest
  {
    public int Amount { get; set; }      // céntimos
    public string? Currency { get; set; } // "eur"
    public string? UserId { get; set; }   // viene de Flutter
    public double Coins { get; set; }    // cantidad a dar tras pagar
  }



 
}
