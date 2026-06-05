using MediatR;
using BetsTrading.Application.Interfaces;
using BetsTrading.Domain.Interfaces;
using Stripe;

namespace BetsTrading.Application.Commands.Payments;

public class CreatePaymentIntentCommandHandler : IRequestHandler<CreatePaymentIntentCommand, CreatePaymentIntentResult>
{
    private readonly IApplicationLogger _logger;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IAdminRuntimeConfig _adminConfig;
    private readonly ICoinPurchasePricingService _coinPricing;

    public CreatePaymentIntentCommandHandler(
        IApplicationLogger logger,
        IUnitOfWork unitOfWork,
        IAdminRuntimeConfig adminConfig,
        ICoinPurchasePricingService coinPricing)
    {
        _logger = logger;
        _unitOfWork = unitOfWork;
        _adminConfig = adminConfig;
        _coinPricing = coinPricing;
        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
    }

    public async Task<CreatePaymentIntentResult> Handle(CreatePaymentIntentCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var productType = (request.ProductType ?? "coins").Trim();
            long amountMinor = request.Amount;
            var coins = request.Coins;
            var currency = (request.Currency ?? "eur").ToLowerInvariant();

            if (!string.Equals(productType, "no_ads", StringComparison.OrdinalIgnoreCase))
            {
                if (!_coinPricing.TryResolvePackage(currency, coins, amountMinor, out var package))
                {
                    return new CreatePaymentIntentResult
                    {
                        Success = false,
                        Message = "Invalid coin package"
                    };
                }

                amountMinor = (long)Math.Round(package.Price * 100.0, MidpointRounding.AwayFromZero);
                coins = package.Coins;
            }

            if (string.Equals(productType, "no_ads", StringComparison.OrdinalIgnoreCase))
            {
                var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
                if (user == null)
                {
                    return new CreatePaymentIntentResult { Success = false, Message = "User not found" };
                }

                if (user.NoAds)
                {
                    return new CreatePaymentIntentResult { Success = false, Message = "no_ads_already_owned" };
                }

                var price = currency == "usd"
                    ? (_adminConfig.NoAdsPriceUsd ?? 4.99)
                    : (_adminConfig.NoAdsPriceEur ?? 4.99);
                amountMinor = (long)Math.Round(price * 100.0, MidpointRounding.AwayFromZero);
                coins = 0;
            }

            var options = new PaymentIntentCreateOptions
            {
                Amount = amountMinor,
                Currency = currency,
                AutomaticPaymentMethods = new PaymentIntentAutomaticPaymentMethodsOptions
                {
                    Enabled = true
                },
                Metadata = new Dictionary<string, string>
                {
                    { "userId", request.UserId },
                    { "coins", coins.ToString() },
                    { "productType", string.Equals(productType, "no_ads", StringComparison.OrdinalIgnoreCase) ? "no_ads" : "coins" }
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
