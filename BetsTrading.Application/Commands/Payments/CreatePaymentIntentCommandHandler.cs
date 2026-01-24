using MediatR;
using BetsTrading.Application.Interfaces;
using Stripe;

namespace BetsTrading.Application.Commands.Payments;

public class CreatePaymentIntentCommandHandler : IRequestHandler<CreatePaymentIntentCommand, CreatePaymentIntentResult>
{
    private readonly IApplicationLogger _logger;

    public CreatePaymentIntentCommandHandler(IApplicationLogger logger)
    {
        _logger = logger;
        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
    }

    public async Task<CreatePaymentIntentResult> Handle(CreatePaymentIntentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var options = new PaymentIntentCreateOptions
            {
                Amount = request.Amount,
                Currency = request.Currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true
                },
                Metadata = new Dictionary<string, string>
                {
                    { "userId", request.UserId },
                    { "coins", request.Coins.ToString() }
                }
            };

            var service = new PaymentIntentService();
            var intent = await service.CreateAsync(options, cancellationToken: cancellationToken);

            return new CreatePaymentIntentResult
            {
                Success = true,
                ClientSecret = intent.ClientSecret
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[PAYMENTS] :: CreatePaymentIntent :: Error: {0}", ex.Message);
            return new CreatePaymentIntentResult
            {
                Success = false,
                Message = ex.Message
            };
        }
    }
}
