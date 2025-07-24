namespace BetsTrading_Service.Controllers
{
  using Microsoft.AspNetCore.Mvc;
  using Stripe;

  [ApiController]
  [Route("api/[controller]")]
  public class PaymentsController : ControllerBase
  {
    private readonly IConfiguration _config;

    public PaymentsController(IConfiguration config)
    {
      _config = config;
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
          PaymentMethodTypes = new List<string> { "card"  },
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
  }

  public class CreatePaymentIntentRequest
  {
    public int Amount { get; set; }      // en céntimos
    public string Currency { get; set; } // ej: "eur"
  }

}
