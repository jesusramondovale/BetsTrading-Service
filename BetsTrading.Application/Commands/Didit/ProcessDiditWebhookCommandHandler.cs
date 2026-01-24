using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;
using BetsTrading.Application.Services;

namespace BetsTrading.Application.Commands.Didit;

public class ProcessDiditWebhookCommandHandler : IRequestHandler<ProcessDiditWebhookCommand, ProcessDiditWebhookResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDiditApiService _diditApiService;
    private readonly IEmailService _emailService;
    private readonly ILocalizationService _localizationService;
    private readonly IApplicationLogger _logger;

    public ProcessDiditWebhookCommandHandler(
        IUnitOfWork unitOfWork,
        IDiditApiService diditApiService,
        IEmailService emailService,
        ILocalizationService localizationService,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _diditApiService = diditApiService;
        _emailService = emailService;
        _localizationService = localizationService;
        _logger = logger;
    }

    public async Task<ProcessDiditWebhookResult> Handle(ProcessDiditWebhookCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var payload = request.Payload;

            // Extract vendor_data (user ID)
            string? vendorData = payload.TryGetProperty("vendor_data", out var vendorProp)
                ? vendorProp.GetString()
                : null;

            if (string.IsNullOrEmpty(vendorData))
            {
                _logger.Warning("[DIDIT] :: Webhook without vendor_data");
                return new ProcessDiditWebhookResult
                {
                    Success = false,
                    Message = "Missing vendor_data"
                };
            }

            var user = await _unitOfWork.Users.GetByIdAsync(vendorData, cancellationToken);
            if (user == null)
            {
                _logger.Warning("[DIDIT] :: Webhook for non-existent user {0}", vendorData);
                return new ProcessDiditWebhookResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            // Update session ID if provided
            if (payload.TryGetProperty("session_id", out var sidProp))
            {
                var sessionId = sidProp.GetString();
                if (!string.IsNullOrEmpty(sessionId))
                {
                    user.UpdateDiditSessionId(sessionId);
                    _logger.Information("[DIDIT] :: Updated user {0} with session {1}", user.Id, sessionId);
                }
            }

            // Process status updates
            string webhookType = payload.TryGetProperty("webhook_type", out var wtProp)
                ? wtProp.GetString() ?? ""
                : "";

            string status = payload.TryGetProperty("status", out var stProp)
                ? stProp.GetString() ?? ""
                : "";

            if (webhookType == "status.updated" && (status == "Approved" || status == "Declined"))
            {
                _logger.Information("[DIDIT] :: Status update for user {0}: {1}", user.Id, status);

                if (!string.IsNullOrEmpty(user.DiditSessionId))
                {
                    var decision = await _diditApiService.GetSessionDecisionAsync(user.DiditSessionId, cancellationToken);
                    
                    if (decision?.IdVerification != null)
                    {
                        var idVer = decision.IdVerification;

                        // Check age
                        if (idVer.Age.HasValue && idVer.Age.Value < 18)
                        {
                            _logger.Warning("[DIDIT] :: User {0} is below legal age ({1} y-o)", user.Id, idVer.Age.Value);
                            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                            return new ProcessDiditWebhookResult
                            {
                                Success = false,
                                Message = "User under legal age"
                            };
                        }

                        // Update full name
                        if (!string.IsNullOrEmpty(idVer.FullName))
                        {
                            // Note: User entity doesn't have a method to update Fullname, we'll need to add it or set it directly
                            // For now, assuming we can set it directly (it's a public property)
                            user.Fullname = idVer.FullName;
                            _logger.Information("[DIDIT] :: User {0} full name set to {1}", user.Id, idVer.FullName);
                        }

                        // Update country
                        if (!string.IsNullOrEmpty(idVer.IssuingStateName))
                        {
                            var countryCode = CountryCodeMapper.GetCountryCodeByName(idVer.IssuingStateName);
                            user.Country = countryCode;
                            _logger.Information("[DIDIT] :: User {0} country set to {1}", user.Id, idVer.IssuingStateName);
                        }

                        // Update date of birth
                        if (idVer.DateOfBirth.HasValue)
                        {
                            user.Birthday = idVer.DateOfBirth.Value;
                            _logger.Information("[DIDIT] :: User {0} DOB set to {1}", user.Id, idVer.DateOfBirth.Value);
                        }

                        // Update verification status
                        if (status == "Approved")
                        {
                            user.IsVerified = true;

                            // Send verification email
                            try
                            {
                                var countryCode = user.Country ?? "GB";
                                var emailBodyTemplate = _localizationService.GetTranslationByCountry(countryCode, "userVerifiedEmailBody");
                                var emailBody = string.Format(emailBodyTemplate, user.Fullname ?? user.Username);
                                var emailSubject = _localizationService.GetTranslationByCountry(countryCode, "emailSubjectUserVerified");

                                await _emailService.SendEmailAsync(
                                    to: user.Email,
                                    subject: emailSubject,
                                    body: emailBody
                                );
                            }
                            catch (Exception ex)
                            {
                                _logger.Warning("[DIDIT] :: Failed to send verification email to user {0}: {1}", user.Id, ex.Message);
                            }

                            _logger.Information("[DIDIT] :: User {0} verified", user.Id);
                        }
                        else
                        {
                            user.IsVerified = false;
                            _logger.Warning("[DIDIT] :: User {0} verification declined", user.Id);
                        }
                    }
                }
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new ProcessDiditWebhookResult
            {
                Success = true,
                Message = "Webhook processed"
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.Error(ex, "[DIDIT] :: Webhook error");
            return new ProcessDiditWebhookResult
            {
                Success = false,
                Message = "Internal server error"
            };
        }
    }
}
