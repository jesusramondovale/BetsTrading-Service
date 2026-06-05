using BetsTrading.Application.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using Stripe;

namespace BetsTrading.Infrastructure.Services;

public class StripePaymentFulfillmentService : IStripePaymentFulfillmentService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public StripePaymentFulfillmentService(IUnitOfWork unitOfWork, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
        StripeConfiguration.ApiKey = Environment.GetEnvironmentVariable("STRIPE_SECRET_KEY") ?? "";
    }

    public async Task<PaymentFulfillmentResult> TryFulfillPaymentIntentAsync(
        string paymentIntentId,
        string? expectedUserId = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(paymentIntentId))
        {
            return new PaymentFulfillmentResult { Success = false, Message = "payment_intent_id required" };
        }

        var existingPayment = await _unitOfWork.PaymentData.GetByPaymentIntentIdAsync(paymentIntentId, cancellationToken);
        if (existingPayment != null)
        {
            return new PaymentFulfillmentResult
            {
                Success = true,
                AlreadyProcessed = true,
                CoinsCredited = existingPayment.Coins,
                Message = "already_processed"
            };
        }

        var service = new PaymentIntentService();
        PaymentIntent intent;
        try
        {
            intent = await service.GetAsync(paymentIntentId, cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[Stripe] Fulfillment :: Could not retrieve PaymentIntent {0}", paymentIntentId);
            return new PaymentFulfillmentResult { Success = false, Message = "payment_intent_not_found" };
        }

        if (!string.Equals(intent.Status, "succeeded", StringComparison.OrdinalIgnoreCase))
        {
            return new PaymentFulfillmentResult
            {
                Success = false,
                Message = $"payment_not_succeeded:{intent.Status}"
            };
        }

        if (!intent.Metadata.TryGetValue("userId", out var userId) || string.IsNullOrWhiteSpace(userId))
        {
            _logger.Warning("[Stripe] Fulfillment :: PaymentIntent {0} missing userId metadata", paymentIntentId);
            return new PaymentFulfillmentResult { Success = false, Message = "missing_user_id" };
        }

        if (!string.IsNullOrWhiteSpace(expectedUserId) &&
            !string.Equals(userId, expectedUserId, StringComparison.Ordinal))
        {
            return new PaymentFulfillmentResult { Success = false, Message = "user_mismatch" };
        }

        var productType = intent.Metadata.TryGetValue("productType", out var pt) ? pt : "coins";
        var (paymentMethod, currency, amount) = await ResolveChargeDetailsAsync(intent, cancellationToken);

        var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);
        if (user == null)
        {
            _logger.Warning("[Stripe] Fulfillment :: User not found {0} for PaymentIntent {1}", userId, paymentIntentId);
            return new PaymentFulfillmentResult { Success = false, Message = "user_not_found" };
        }

        if (string.Equals(productType, "no_ads", StringComparison.OrdinalIgnoreCase))
        {
            if (!user.NoAds)
                user.GrantNoAds();

            await _unitOfWork.PaymentData.AddAsync(new PaymentData(
                userId, intent.Id, 0, currency, amount, true, paymentMethod), cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.Information("[Stripe] Fulfillment :: No-ads granted user={0} pi={1}", userId, paymentIntentId);
            return new PaymentFulfillmentResult { Success = true, Message = "no_ads_granted" };
        }

        var coins = 0.0;
        if (intent.Metadata.TryGetValue("coins", out var coinsRaw) &&
            double.TryParse(coinsRaw, System.Globalization.CultureInfo.InvariantCulture, out var parsedCoins))
        {
            coins = parsedCoins;
        }

        if (coins <= 0)
        {
            _logger.Warning("[Stripe] Fulfillment :: PaymentIntent {0} has zero coins in metadata", paymentIntentId);
            return new PaymentFulfillmentResult { Success = false, Message = "zero_coins" };
        }

        if (!await _unitOfWork.Users.TryAddPointsAsync(userId, coins, cancellationToken))
        {
            return new PaymentFulfillmentResult { Success = false, Message = "could_not_credit_points" };
        }

        await _unitOfWork.PaymentData.AddAsync(new PaymentData(
            userId, intent.Id, coins, currency, amount, true, paymentMethod), cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.Information("[Stripe] Fulfillment :: Credited {0} coins to user={1} pi={2}", coins, userId, paymentIntentId);
        return new PaymentFulfillmentResult
        {
            Success = true,
            CoinsCredited = coins,
            Message = "coins_credited"
        };
    }

    private static async Task<(string paymentMethod, string currency, double amount)> ResolveChargeDetailsAsync(
        PaymentIntent intent,
        CancellationToken cancellationToken)
    {
        string paymentMethod = "unknown";
        var currency = intent.Currency ?? "unknown";
        double amount = intent.Amount / 100.0;

        try
        {
            var chargeService = new ChargeService();
            Charge? charge = null;

            if (!string.IsNullOrEmpty(intent.LatestChargeId))
                charge = await chargeService.GetAsync(intent.LatestChargeId, cancellationToken: cancellationToken);
            else
            {
                var list = await chargeService.ListAsync(new ChargeListOptions
                {
                    PaymentIntent = intent.Id,
                    Limit = 1
                }, cancellationToken: cancellationToken);
                charge = list.Data.FirstOrDefault();
            }

            if (charge?.PaymentMethodDetails != null)
            {
                var pmd = charge.PaymentMethodDetails;
                currency = charge.Currency;
                amount = charge.Amount / 100.0;
                paymentMethod = pmd.Type == "card" && pmd.Card != null
                    ? $"{pmd.Card.Brand} ****{pmd.Card.Last4}"
                    : pmd.Type ?? "unknown";
            }
        }
        catch
        {
            // Keep defaults from PaymentIntent
        }

        return (paymentMethod, currency, amount);
    }
}
