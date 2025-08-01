namespace BetsTrading_Service.Controllers
{
  using BetsTrading_Service.Database;
  using BetsTrading_Service.Interfaces;
  using BetsTrading_Service.Requests;
  using Microsoft.AspNetCore.Mvc;
  using Microsoft.EntityFrameworkCore;
  using Stripe;
  using System.Security.Cryptography;
  using System.Text.Json;

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
      StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY", EnvironmentVariableTarget.User);
    }

    [HttpPost("CreatePaymentIntent")]
    public IActionResult CreatePaymentIntent([FromBody] CreatePaymentIntentRequest req)
    {
      try
      {

        var options = new PaymentIntentCreateOptions
        {
          Amount = req.Amount,
          Currency = req.Currency,
          PaymentMethodTypes = new List<string> { "card" },
        };

        var service = new PaymentIntentService();
        var intent = service.Create(options);

        return Ok(new
        {
          client_secret = intent.ClientSecret
        });
      }
      catch (Exception ex)
      {
        return StatusCode(500, new
        {
          message = "Stripe error",
          error = ex.Message
        });
      }
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
            user.points += double.Parse(reward_amount);
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
  }

  public class CreatePaymentIntentRequest
  {
    public int Amount { get; set; }      // en céntimos
    public string Currency { get; set; } // ej: "eur"
  }

}
