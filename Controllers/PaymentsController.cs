namespace BetsTrading_Service.Controllers
{
  using BetsTrading_Service.Database;
  using BetsTrading_Service.Interfaces;
  using BetsTrading_Service.Locale;
  using BetsTrading_Service.Models;
  using BetsTrading_Service.Requests;
  using BetsTrading_Service.Services;
  using Google.Apis.Auth.OAuth2.Requests;
  using Microsoft.AspNetCore.Authorization;
  using Microsoft.AspNetCore.Identity.Data;
  using Microsoft.AspNetCore.Mvc;
  using Microsoft.EntityFrameworkCore;
  using Newtonsoft.Json;
  using Stripe;
  using Stripe.Forwarding;
  using Stripe.V2;
  using System.Drawing;
  using System.IdentityModel.Tokens.Jwt;
  using System.IO;
  using System.Runtime.CompilerServices;
  using System.Security.Claims;
  using System.Security.Cryptography;
  using System.Text;
  using System.Text.Json;
  using System.Text.Json.Serialization;


  [ApiController]
  [Route("api/[controller]")]
  public class PaymentsController : ControllerBase
  {
    private readonly IConfiguration _config;
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;
    private readonly IEmailService _emailService;

    public PaymentsController(AppDbContext dbContext, IConfiguration config, ICustomLogger customLogger, IEmailService emailService)
    {
      _dbContext = dbContext;
      _config = config;
      _logger = customLogger;
      _emailService = emailService;
      StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY", EnvironmentVariableTarget.User) ?? "";
      //TO-DO -> Use real Key (already added to env variables)
      //StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_REAL_SECRET_KEY", EnvironmentVariableTarget.User) ?? "";

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

    private static byte[] Base64UrlDecode(string s)
    {
      s = s.Replace('-', '+').Replace('_', '/');
      switch (s.Length % 4) { case 2: s += "=="; break; case 3: s += "="; break; case 0: break; default: throw new FormatException("bad b64url"); }
      return Convert.FromBase64String(s);
    }

    [HttpPost("CreatePaymentIntent")]
    public IActionResult CreatePaymentIntent([FromBody] CreatePaymentIntentRequest req)
    {

      try
      {
        var tokenUserId =
          User.FindFirstValue(ClaimTypes.NameIdentifier) ??
          User.FindFirstValue("app_sub") ??
          User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
          return Unauthorized(new { Message = "Invalid token" });

        if (!string.IsNullOrEmpty(req.UserId) &&
            !string.Equals(req.UserId, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
          // 403 FORBIDDEN : User ID doesn't match JWT
          return Forbid();
        }

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

    [AllowAnonymous]
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

          // ==== Extraer método de pago ====
          string paymentMethod = "unknown";
          var currency = "unknown";
          double amount = 0;
          var chargeService = new ChargeService();
          Charge? charge = null;

          if (!string.IsNullOrEmpty(intent.LatestChargeId))
          {
            charge = await chargeService.GetAsync(intent.LatestChargeId);
          }
          else
          {
            var list = await chargeService.ListAsync(new ChargeListOptions
            {
              PaymentIntent = intent.Id,
              Limit = 1
            });
            charge = list.Data.FirstOrDefault();
          }

          if (charge?.PaymentMethodDetails != null)
          {
            var pmd = charge.PaymentMethodDetails;
            currency = charge.Currency;
            amount = charge.Amount/100.0;

            if (pmd.Type == "card" && pmd.Card != null)
            {
              paymentMethod = $"{pmd.Card.Brand} ****{pmd.Card.Last4}";
            }
            else
            {
              paymentMethod = pmd.Type ?? "unknown";
            }


          }

          _logger.Log.Information($"[Stripe] Pay confirmed for user {userId} ({coins} coins) via {paymentMethod}");

          var user = _dbContext.Users.FirstOrDefault(u => u.id == userId);
          if (user != null)
          {
            user.points += coins;

            var paymentHistory = new PaymentData(
              Guid.NewGuid(),
              userId,
              intent.Id,
              coins,
              currency,
              amount,
              DateTime.UtcNow,
              true,
              paymentMethod
            );

            _dbContext.PaymentData.Add(paymentHistory);
            await _dbContext.SaveChangesAsync();
          }
        }
        else if (stripeEvent.Type == "payment_intent.payment_failed")
        {
          _logger.Log.Warning("[Stripe] Pago fallido.");

          var intent = stripeEvent.Data.Object as PaymentIntent;
          var userId = intent?.Metadata["userId"];

          // ==== Extraer método de pago (igual que arriba) ====
          string paymentMethod = "unknown";
          var chargeService = new ChargeService();
          Charge? charge = null;

          if (!string.IsNullOrEmpty(intent?.LatestChargeId))
          {
            charge = await chargeService.GetAsync(intent.LatestChargeId);
          }
          else if (intent != null)
          {
            var list = await chargeService.ListAsync(new ChargeListOptions
            {
              PaymentIntent = intent.Id,
              Limit = 1
            });
            charge = list.Data.FirstOrDefault();
          }

          if (charge?.PaymentMethodDetails != null)
          {
            var pmd = charge.PaymentMethodDetails;
            if (pmd.Type == "card" && pmd.Card != null)
            {
              paymentMethod = $"{pmd.Card.Brand} ****{pmd.Card.Last4}";
            }
            else
            {
              paymentMethod = pmd.Type ?? "unknown";
            }
          }
          // ===============================================

          if (!string.IsNullOrEmpty(userId))
          {
            var paymentHistory = new PaymentData(
              Guid.NewGuid(),
              userId,
              intent?.Id ?? Guid.NewGuid().ToString(),
              0,
              "EUR",
              0,
              DateTime.UtcNow,
              false,
              paymentMethod
            );

            _dbContext.PaymentData.Add(paymentHistory);
            await _dbContext.SaveChangesAsync();
          }
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
          var tokenUserId =
            User.FindFirstValue(ClaimTypes.NameIdentifier) ??
            User.FindFirstValue("app_sub") ??
            User.FindFirstValue(JwtRegisteredClaimNames.Sub);

          if (string.IsNullOrEmpty(tokenUserId))
            return Unauthorized(new { Message = "Invalid token" });

          if (!string.IsNullOrEmpty(req.UserId) &&
              !string.Equals(req.UserId, tokenUserId, StringComparison.Ordinal) &&
              !User.IsInRole("admin"))
          {
            // 403 FORBIDDEN : User ID doesn't match JWT
            return Forbid();
          }

          string? ip = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault() ?? HttpContext.Connection.RemoteIpAddress?.ToString();
          var geo = await AuthController.GetGeoLocationFromIp(ip!);

          var user = _dbContext.Users.FirstOrDefault(u => u.fcm == req.fcm && u.id == req.UserId);
          if (user != null)
          {
            if (!BCrypt.Net.BCrypt.Verify(req.Password, user.password))
            {
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
            var options = System.Text.Json.JsonSerializer.Deserialize<List<ExchangeOption>>(optionsJson);

            if (!options!.Any(o => o.Type == "exchange" && o.Coins == req.Coins && o.Euros == req.CurrencyAmount))
            {
              _logger.Log.Warning("[PAYMENTS] :: RetireBalance :: Invalid coins/amount combination: {coins} - {euros}", req.Coins, req.CurrencyAmount);
              return BadRequest(new { Message = "Invalid coins/amount combination." });
            }

            user.pending_balance += req.CurrencyAmount;
            user.points -= req.Coins;
            
            var withdrawalHistory = new WithdrawalData(
              Guid.NewGuid(),
              req.UserId!,
              req.Coins,
              req.Currency!,
              req.CurrencyAmount,
              DateTime.UtcNow,
              false,
              req.method!);

            _dbContext.WithdrawalData.Add(withdrawalHistory);

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            
            var parts = req.method!.Split('#');
            string methodType = parts[0];       // ej: withdrawMethodPaypal
            string methodId = parts.Length > 1 ? parts[1] : string.Empty;
            
            var method = await _dbContext.WithdrawalMethods
                .FirstOrDefaultAsync(m => m.Id.ToString() == methodId);

            string methodDetails = methodType; // fallback

            if (method != null)
            {
              string json = method.Data.RootElement.GetRawText();

              dynamic data = JsonConvert.DeserializeObject(json);

              switch (method.Type.ToLower()) // si tienes una columna 'type'
              {
                case "bank":
                  methodDetails =
                      $"Bank transfer\n" +
                      $"Holder: {data!.holder}\n" +
                      $"IBAN: {data!.iban}\n" +
                      (data.bic != null ? $"BIC: {data.bic}\n" : "");
                  break;

                case "paypal":
                  methodDetails = $"PayPal account: {data!.email}";
                  break;

                case "crypto":
                  methodDetails =
                      $"Crypto\n" +
                      $"Address: {data!.address}\n" +
                      $"Network: {data!.network}";
                  break;

                default:
                  methodDetails = method.Type;
                  break;
              }
            }
            
            string localedBodyTemplate = LocalizedTexts.GetTranslationByCountry(user.country, "withdrawalEmailBody");

            string localedBody = geo != null
                ? string.Format(localedBodyTemplate, user.fullname, req.CurrencyAmount, req.Currency!.ToUpper(), methodDetails, geo.City, geo.RegionName, geo.Country)
                : string.Format(localedBodyTemplate, user.fullname, req.CurrencyAmount, req.Currency!.ToUpper(), methodDetails, "?", "", "");

            await _emailService.SendEmailAsync(
                to: user.email,
                subject: "Betrader payment",
                body: localedBody
            );

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

    [AllowAnonymous]
    [HttpGet("VerifyAd")]
    public async Task<IActionResult> Verify()
    {
      var raw = Request.QueryString.Value;
      if (string.IsNullOrEmpty(raw)) return BadRequest("empty query");

      var sigB64u = Request.Query["signature"].ToString();
      var keyIdTxt = Request.Query["key_id"].ToString();
      if (string.IsNullOrEmpty(sigB64u) || string.IsNullOrEmpty(keyIdTxt))
        return BadRequest();

      if (!ulong.TryParse(keyIdTxt, out var keyId))
        return Unauthorized("invalid key_id");
      
      var query = raw.TrimStart('?');
      var iSig = query.IndexOf("signature=", StringComparison.Ordinal);
      if (iSig < 0) return Ok();
      var toVerify = query.Substring(0, iSig - 1);
      var data = Encoding.UTF8.GetBytes(toVerify);

      // PEM by keyId
      using var http = new HttpClient();
      var json = await http.GetStringAsync("https://www.gstatic.com/admob/reward/verifier-keys.json");
      using var doc = System.Text.Json.JsonDocument.Parse(json);
      var keys = doc.RootElement.GetProperty("keys");

      string? pem = null;
      foreach (var k in keys.EnumerateArray())
      {
        if (k.TryGetProperty("keyId", out var kid) &&
            (kid.TryGetUInt64(out var kidU) ? kidU == keyId
             : kid.ValueKind == System.Text.Json.JsonValueKind.String && kid.GetString() == keyIdTxt))
        {
          pem = k.GetProperty("pem").GetString();
          break;
        }
      }
      if (string.IsNullOrEmpty(pem)) return Unauthorized("unknown key");

      // DER on pubKey
      using var ecdsa = ECDsa.Create();
      try { ecdsa.ImportFromPem(pem); }
      catch { return Unauthorized("bad public key"); }

      byte[] sig;
      try { sig = Base64UrlDecode(sigB64u); }
      catch { return Unauthorized("bad signature b64"); }
      
      var ok = ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
      if (!ok) return Unauthorized("bad signature");
      
      var qp = Request.Query;
      var transactionId = qp["transaction_id"].ToString();
      var userId = qp["user_id"].ToString();
      var customData = qp["custom_data"].ToString();
      var rewardAmountS = qp["reward_amount"].ToString();
      var rewardItem = qp["reward_item"].ToString();
      var adUnitIdStr = qp["ad_unit"].ToString();

      var nonce = await _dbContext.RewardNonces.SingleOrDefaultAsync(n => n.Nonce == customData);
      if (nonce is null || nonce.Used || nonce.ExpiresAt < DateTime.UtcNow || nonce.UserId != userId)
        return BadRequest("invalid_nonce");

      if (await _dbContext.RewardTransactions.AnyAsync(t => t.TransactionId == transactionId))
        return Ok();

      if (!double.TryParse(rewardAmountS, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out var rewardAmount))
        rewardAmount = 0;

      using var tx = await _dbContext.Database.BeginTransactionAsync();
      var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.id == userId);
      if (user == null) { await tx.RollbackAsync(); return NotFound(new { Message = "User not found" }); }

      user.points += (double)rewardAmount;
      nonce.Used = true;
      _dbContext.RewardTransactions.Add(new RewardTransaction
      {
        TransactionId = transactionId,
        UserId = userId,
        Coins = (decimal)rewardAmount,
        AdUnitId = adUnitIdStr,
        RewardItem = rewardItem,
        RewardAmountRaw = rewardAmount,
        SsvKeyId = (int?)(keyId <= int.MaxValue ? (int)keyId : null),
        CreatedAt = DateTime.UtcNow,
        RawQuery = query
      });
      await _dbContext.SaveChangesAsync();
      await tx.CommitAsync();

      return Ok();
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
    public string? method{ get; set; }
  }

  public class CreatePaymentIntentRequest
  {
    public int Amount { get; set; }
    public string? Currency { get; set; }
    public string? UserId { get; set; }
    public double Coins { get; set; }
  }
}
