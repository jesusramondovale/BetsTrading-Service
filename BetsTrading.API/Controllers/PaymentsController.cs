using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using MediatR;
using BetsTrading.Application.Commands.Payments;
using BetsTrading.Application.Commands.Rewards;
using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;
using Stripe;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Application.Interfaces;
using BetsTrading.API.Security;

namespace BetsTrading.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class PaymentsController : ControllerBase
{
    private readonly IMediator _mediator;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly IApplicationLogger _logger;
    private readonly IStepUpTokenService _stepUpTokenService;
    private readonly IStripePaymentFulfillmentService _paymentFulfillment;

    public PaymentsController(
        IMediator mediator, 
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        IApplicationLogger logger,
        IStepUpTokenService stepUpTokenService,
        IStripePaymentFulfillmentService paymentFulfillment)
    {
        _mediator = mediator;
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _logger = logger;
        _stepUpTokenService = stepUpTokenService;
        _paymentFulfillment = paymentFulfillment;
        // Stripe se configura en Program.cs
    }

    [HttpPost("CreatePaymentIntent")]
    public async Task<IActionResult> CreatePaymentIntent([FromBody] CreatePaymentIntentCommand command, CancellationToken cancellationToken)
    {
        var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("app_sub") 
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
        {
            return Unauthorized(new { Message = "Invalid token" });
        }

        if (!string.IsNullOrEmpty(command.UserId) &&
            !string.Equals(command.UserId, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
            return Forbid();
        }

        command.UserId = tokenUserId;
        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message is "User not found" or "no_ads_already_owned" or "Invalid coin package")
            {
                return BadRequest(new { message = result.Message });
            }

            return StatusCode(500, new { message = "Stripe error", error = result.Message });
        }

        return Ok(new { client_secret = result.ClientSecret });
    }

    [HttpPost("ConfirmPaymentIntent")]
    public async Task<IActionResult> ConfirmPaymentIntent(
        [FromBody] ConfirmPaymentIntentRequest? request,
        CancellationToken cancellationToken)
    {
        var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? User.FindFirstValue("app_sub")
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
            return Unauthorized(new { Message = "Invalid token" });

        var paymentIntentId = request?.PaymentIntentId?.Trim();
        if (string.IsNullOrWhiteSpace(paymentIntentId))
            return BadRequest(new { Message = "paymentIntentId is required" });

        var result = await _paymentFulfillment.TryFulfillPaymentIntentAsync(
            paymentIntentId, tokenUserId, cancellationToken);

        if (!result.Success)
            return BadRequest(new { message = result.Message });

        return Ok(new
        {
            success = true,
            alreadyProcessed = result.AlreadyProcessed,
            coinsCredited = result.CoinsCredited,
            message = result.Message
        });
    }

    [AllowAnonymous]
    [HttpPost("Webhook")]
    public async Task<IActionResult> StripeWebhook()
    {
        var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
        try
        {
            var sigHeader = Request.Headers["Stripe-Signature"];
            var endpointSecret = Environment.GetEnvironmentVariable("STRIPE_WEBHOOK_SECRET") ?? "";

            var stripeEvent = EventUtility.ConstructEvent(json, sigHeader, endpointSecret);

            if (stripeEvent.Type == "payment_intent.succeeded")
            {
                var intent = stripeEvent.Data.Object as PaymentIntent;
                if (intent == null)
                {
                    _logger.Warning("[Stripe] Webhook payment_intent.succeeded with null intent");
                    return BadRequest();
                }

                _logger.Information("[Stripe] Webhook payment_intent.succeeded pi={0}", intent.Id);
                var result = await _paymentFulfillment.TryFulfillPaymentIntentAsync(intent.Id, cancellationToken: default);
                if (!result.Success)
                    _logger.Warning("[Stripe] Webhook fulfillment failed pi={0} msg={1}", intent.Id, result.Message ?? "unknown");
            }
            else if (stripeEvent.Type == "payment_intent.payment_failed")
            {
                _logger.Warning("[Stripe] Payment failed");

                var intent = stripeEvent.Data.Object as PaymentIntent;
                var userId = intent?.Metadata["userId"];

                if (!string.IsNullOrEmpty(userId))
                {
                    var paymentHistory = new PaymentData(
                        userId,
                        intent?.Id ?? Guid.NewGuid().ToString(),
                        0,
                        "EUR",
                        0,
                        false,
                        "unknown"
                    );

                    await _unitOfWork.PaymentData.AddAsync(paymentHistory, cancellationToken: default);
                    await _unitOfWork.SaveChangesAsync();
                }
            }

            return Ok();
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[ERROR] Stripe Webhook");
            return BadRequest();
        }
    }

    [HttpPost("RetireBalance")]
    public async Task<IActionResult> RetireBalance([FromBody] RetireBalanceCommand command, CancellationToken cancellationToken)
    {
        var tokenUserId = User.FindFirstValue(ClaimTypes.NameIdentifier) 
            ?? User.FindFirstValue("app_sub") 
            ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);

        if (string.IsNullOrEmpty(tokenUserId))
        {
            return Unauthorized(new { Message = "Invalid token" });
        }

        if (!string.IsNullOrEmpty(command.UserId) &&
            !string.Equals(command.UserId, tokenUserId, StringComparison.Ordinal) &&
            !User.IsInRole("admin"))
        {
            return Forbid();
        }

        command.UserId = tokenUserId;
        if (!string.IsNullOrWhiteSpace(command.StepUpToken))
        {
            command.StepUpValidated = _stepUpTokenService.ValidateAndConsume(
                command.StepUpToken,
                tokenUserId,
                "withdraw",
                command.Coins);
            if (!command.StepUpValidated)
            {
                return BadRequest(new { Message = "Invalid step-up token" });
            }
        }
        command.ClientIp = HttpContext.Request.Headers["X-Forwarded-For"].FirstOrDefault()
            ?? HttpContext.Connection.RemoteIpAddress?.ToString();

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            return BadRequest(new { Message = result.Message });
        }

        return Ok(new { Message = result.Message });
    }

    [AllowAnonymous]
    [HttpGet("VerifyAd")]
    public async Task<IActionResult> VerifyAd(CancellationToken cancellationToken)
    {
        // Extract parameters from query string (AdMob SSV callback)
        var rawQuery = Request.QueryString.Value;
        if (string.IsNullOrEmpty(rawQuery))
        {
            return BadRequest(new { Message = "empty query" });
        }

        var qp = Request.Query;
        var signatureB64u = qp["signature"].ToString();
        var keyIdTxt = qp["key_id"].ToString();
        var transactionId = qp["transaction_id"].ToString();
        var userId = qp["user_id"].ToString();
        var customData = qp["custom_data"].ToString();
        var rewardAmountS = qp["reward_amount"].ToString();
        var rewardItem = qp["reward_item"].ToString();
        var adUnitIdStr = qp["ad_unit"].ToString();

        if (string.IsNullOrWhiteSpace(signatureB64u) || string.IsNullOrWhiteSpace(keyIdTxt))
        {
            return BadRequest(new { Message = "Missing signature or key_id" });
        }

        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrWhiteSpace(transactionId) || string.IsNullOrWhiteSpace(customData))
        {
            return BadRequest(new { Message = "Missing required parameters" });
        }

        // Parse reward amount
        double? rewardAmountRaw = null;
        if (!string.IsNullOrWhiteSpace(rewardAmountS) && 
            double.TryParse(rewardAmountS, System.Globalization.NumberStyles.Float,
                           System.Globalization.CultureInfo.InvariantCulture, out var rewardAmount))
        {
            rewardAmountRaw = rewardAmount;
        }

        // Parse key ID
        int? ssvKeyId = null;
        if (!string.IsNullOrWhiteSpace(keyIdTxt) && 
            ulong.TryParse(keyIdTxt, out var keyId) && keyId <= int.MaxValue)
        {
            ssvKeyId = (int)keyId;
        }

        var command = new VerifyAdRewardCommand
        {
            UserId = userId,
            TransactionId = transactionId,
            Nonce = customData,
            AdUnitId = adUnitIdStr,
            RewardItem = rewardItem,
            RewardAmountRaw = rewardAmountRaw,
            SsvKeyId = ssvKeyId,
            RawQuery = rawQuery,
            Signature = signatureB64u,
            KeyId = keyIdTxt
        };

        var result = await _mediator.Send(command, cancellationToken);

        if (!result.Success)
        {
            if (result.Message == "invalid_nonce")
            {
                return BadRequest(new { Message = result.Message });
            }
            if (result.Message == "User not found")
            {
                return NotFound(new { Message = result.Message });
            }
            return BadRequest(new { Message = result.Message ?? "Failed to verify reward" });
        }

        return Ok();
    }
}

public class ConfirmPaymentIntentRequest
{
    public string? PaymentIntentId { get; set; }
}
