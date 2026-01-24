using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Application.Interfaces;
using BCrypt.Net;
using System.Text.Json;

namespace BetsTrading.Application.Commands.Payments;

public class ExchangeOption
{
    public int Coins { get; set; }
    public double Euros { get; set; }
    public string Type { get; set; } = string.Empty;
}

public class RetireBalanceCommandHandler : IRequestHandler<RetireBalanceCommand, RetireBalanceResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IEmailService _emailService;
    private readonly ILocalizationService _localizationService;
    private readonly IApplicationLogger _logger;

    private readonly IIpGeoService _ipGeoService;

    public RetireBalanceCommandHandler(
        IUnitOfWork unitOfWork,
        IEmailService emailService,
        ILocalizationService localizationService,
        IIpGeoService ipGeoService,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _emailService = emailService;
        _localizationService = localizationService;
        _ipGeoService = ipGeoService;
        _logger = logger;
    }

    public async Task<RetireBalanceResult> Handle(RetireBalanceCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var geo = await _ipGeoService.GetGeoFromIpAsync(request.ClientIp, cancellationToken);

            // Get user by FCM and UserId
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            
            if (user == null || user.Fcm != request.Fcm)
            {
                _logger.Warning("[PAYMENTS] :: RetireBalance :: User not found or session expired: {0}", request.UserId);
                return new RetireBalanceResult
                {
                    Success = false,
                    Message = "User not found or session expired"
                };
            }

            // Verify password
            if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                if (geo == null)
                    _logger.Error(null, "[PAYMENTS] :: INCORRECT RETIRE ATTEMPT FOR USER {0} FROM IP {1}", request.UserId, request.ClientIp ?? "UNKNOWN");
                else
                    _logger.Error(null, "[PAYMENTS] :: INCORRECT RETIRE ATTEMPT OF {0} coins FOR USER {1} FROM IP {2} -> {3}, {4}, {5}, ISP: {6}",
                        request.Coins, request.UserId, request.ClientIp ?? "UNKNOWN", geo.City ?? "", geo.RegionName ?? "", geo.Country ?? "", geo.ISP ?? "");
                return new RetireBalanceResult
                {
                    Success = false,
                    Message = "Incorrect password"
                };
            }

            // Validate exchange options
            var path = Path.Combine(Directory.GetCurrentDirectory(), $"exchange_options_{request.Currency.ToLower()}.json");
            if (!File.Exists(path))
            {
                _logger.Warning("[PAYMENTS] :: RetireBalance :: Exchange options file not found: {0}", path);
                return new RetireBalanceResult
                {
                    Success = false,
                    Message = "Exchange options not available"
                };
            }

            var optionsJson = await File.ReadAllTextAsync(path, cancellationToken);
            var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            var options = JsonSerializer.Deserialize<List<ExchangeOption>>(optionsJson, opts);

            if (options == null || !options.Any(o => o.Type == "exchange" && o.Coins == request.Coins && o.Euros == request.CurrencyAmount))
            {
                _logger.Warning("[PAYMENTS] :: RetireBalance :: Invalid coins/amount combination: {0} - {1}", request.Coins, request.CurrencyAmount);
                return new RetireBalanceResult
                {
                    Success = false,
                    Message = "Invalid coins/amount combination"
                };
            }

            // Validate user has enough points
            if (user.Points < request.Coins)
            {
                return new RetireBalanceResult
                {
                    Success = false,
                    Message = "Insufficient points"
                };
            }

            // Update user balance
            user.PendingBalance += request.CurrencyAmount;
            user.DeductPoints(request.Coins);

            // Create withdrawal record
            var withdrawal = new WithdrawalData(
                request.UserId,
                request.Coins,
                request.Currency,
                request.CurrencyAmount,
                false,
                request.Method
            );

            await _unitOfWork.WithdrawalData.AddAsync(withdrawal, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            // Enviar correo de retirada (localizado + geo, igual que legacy)
            _logger.Information("[EMAIL_FLOW] RetireBalance :: Transacción commit OK. Construyendo email. UserId={0}, Geo={1}",
                request.UserId, geo != null ? $"{geo.City},{geo.RegionName},{geo.Country}" : "null");

            var methodDetails = await GetWithdrawalMethodDetailsAsync(request.Method, cancellationToken);
            var country = string.IsNullOrEmpty(user.Country) ? "GB" : user.Country;
            var subject = _localizationService.GetTranslationByCountry(country, "emailSubjectPayment");
            var bodyTemplate = _localizationService.GetTranslationByCountry(country, "withdrawalEmailBody");
            var body = geo != null
                ? string.Format(bodyTemplate, user.Fullname, request.CurrencyAmount, request.Currency.ToUpperInvariant(),
                    methodDetails, geo.City ?? "?", geo.RegionName ?? "", geo.Country ?? "")
                : string.Format(bodyTemplate, user.Fullname, request.CurrencyAmount, request.Currency.ToUpperInvariant(),
                    methodDetails, "?", "", "");

            _logger.Information("[EMAIL_FLOW] RetireBalance :: To={0}, Subject={1}, Country={2}, MethodDetailsLen={3}",
                user.Email ?? "(null)", subject, country, methodDetails?.Length ?? 0);

            if (string.IsNullOrEmpty(user.Email))
            {
                _logger.Warning("[PAYMENTS] :: RetireBalance :: Skipping withdrawal email: user {0} has no email.", request.UserId);
                _logger.Information("[EMAIL_FLOW] RetireBalance :: SKIP (user has no email). UserId={0}", request.UserId);
            }
            else
            {
                try
                {
                    _logger.Information("[EMAIL_FLOW] RetireBalance :: Calling SendEmailAsync to={0}", user.Email);
                    await _emailService.SendEmailAsync(to: user.Email, subject: subject, body: body);
                    _logger.Information("[EMAIL_FLOW] RetireBalance :: SendEmailAsync OK. Email enviado a {0}", user.Email);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "[PAYMENTS] :: RetireBalance :: Failed to send withdrawal email: {0}", ex.Message);
                    _logger.Warning("[PAYMENTS] :: RetireBalance :: Check SMTP config (Host, Username, SMTP__Password in .env). UserId: {0}, Email: {1}", request.UserId, user.Email);
                    if (ex.Message.Contains("Authentication", StringComparison.OrdinalIgnoreCase) || ex.Message.Contains("5.7.0"))
                        _logger.Warning("[PAYMENTS] :: RetireBalance :: Gmail requiere Contraseña de aplicación (no la de la cuenta). Crear en: https://myaccount.google.com/apppasswords");
                    _logger.Information("[EMAIL_FLOW] RetireBalance :: SendEmailAsync FAILED. To={0}, Exception={1}", user.Email, ex.Message);
                }
            }

            _logger.Information("[PAYMENTS] :: RetireBalance :: Success with User ID {0}", request.UserId);

            var symbol = string.Equals(request.Currency, "EUR", StringComparison.OrdinalIgnoreCase) ? "€" : request.Currency;
            return new RetireBalanceResult
            {
                Success = true,
                Message = $"Retired {request.CurrencyAmount}{symbol} ({request.Coins} coins) of user {request.UserId} successfully"
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.Error(ex, "[PAYMENTS] :: RetireBalance :: Internal Server Error: {0}", ex.Message);
            return new RetireBalanceResult
            {
                Success = false,
                Message = "Internal server error"
            };
        }
    }

    private async Task<string> GetWithdrawalMethodDetailsAsync(string method, CancellationToken cancellationToken)
    {
        var parts = method.Split('#');
        if (parts.Length < 2 || !Guid.TryParse(parts[1].Trim(), out var methodGuid))
            return method;

        var list = await _unitOfWork.WithdrawalMethods.FindAsync(m => m.Id == methodGuid, cancellationToken);
        var wm = list.FirstOrDefault();
        if (wm == null)
            return method;

        try
        {
            var r = wm.Data.RootElement;
            return wm.Type.ToLowerInvariant() switch
            {
                "bank" => "Bank transfer\n" +
                    "Holder: " + (r.TryGetProperty("holder", out var h) ? h.GetString() ?? "?" : "?") + "\n" +
                    "IBAN: " + (r.TryGetProperty("iban", out var i) ? i.GetString() ?? "?" : "?") + "\n" +
                    (r.TryGetProperty("bic", out var b) && !string.IsNullOrEmpty(b.GetString()) ? "BIC: " + b.GetString() + "\n" : ""),
                "paypal" => "PayPal account: " + (r.TryGetProperty("email", out var e) ? e.GetString() ?? "?" : "?"),
                "crypto" => "Crypto\nAddress: " + (r.TryGetProperty("address", out var a) ? a.GetString() ?? "?" : "?") +
                    "\nNetwork: " + (r.TryGetProperty("network", out var n) ? n.GetString() ?? "?" : "?"),
                _ => wm.Type
            };
        }
        catch
        {
            return method;
        }
    }
}
