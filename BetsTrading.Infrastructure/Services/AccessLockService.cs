using BetsTrading.Application.Interfaces;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Infrastructure.Services;

public sealed class AccessLockService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFirebaseNotificationService _firebase;
    private readonly AdminRuntimeConfig _adminConfig;
    private readonly IApplicationLogger _logger;

    public AccessLockService(
        IUnitOfWork unitOfWork,
        IFirebaseNotificationService firebase,
        AdminRuntimeConfig adminConfig,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _firebase = firebase;
        _adminConfig = adminConfig;
        _logger = logger;
    }

    public async Task<AccessLockToggleResult> SetAccessLockAsync(bool enabled, CancellationToken cancellationToken = default)
    {
        var fcmTokens = enabled
            ? await _unitOfWork.Users.GetActiveSessionFcmTokensAsync(cancellationToken)
            : Array.Empty<string>();

        _adminConfig.SetAccessLockEnabled(enabled);

        var sessionsInvalidated = 0;
        var logoutNotificationsSent = 0;

        if (enabled)
        {
            sessionsInvalidated = await _unitOfWork.Users.InvalidateAllActiveSessionsAsync(cancellationToken);
            logoutNotificationsSent = await _firebase.BroadcastMaintenanceLogoutAsync(fcmTokens, cancellationToken);
            _logger.Warning(
                "[ADMIN] :: Access lock ENABLED. Sessions invalidated: {Sessions}, FCM logout sent: {Fcm}",
                sessionsInvalidated,
                logoutNotificationsSent);
        }
        else
        {
            _logger.Warning("[ADMIN] :: Access lock DISABLED.");
        }

        return new AccessLockToggleResult
        {
            AccessLockEnabled = _adminConfig.AccessLockEnabled,
            SessionsInvalidated = sessionsInvalidated,
            LogoutNotificationsSent = logoutNotificationsSent,
        };
    }
}

public sealed class AccessLockToggleResult
{
    public bool AccessLockEnabled { get; init; }
    public int SessionsInvalidated { get; init; }
    public int LogoutNotificationsSent { get; init; }
}
