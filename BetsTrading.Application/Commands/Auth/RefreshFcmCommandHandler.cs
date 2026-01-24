using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Commands.Auth;

public class RefreshFcmCommandHandler : IRequestHandler<RefreshFcmCommand, RefreshFcmResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IFirebaseNotificationService _firebaseNotification;
    private readonly IApplicationLogger _logger;

    private readonly IIpGeoService _ipGeoService;

    public RefreshFcmCommandHandler(IUnitOfWork unitOfWork, IFirebaseNotificationService firebaseNotification,
        IIpGeoService ipGeoService, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _firebaseNotification = firebaseNotification;
        _ipGeoService = ipGeoService;
        _logger = logger;
    }

    public async Task<RefreshFcmResult> Handle(RefreshFcmCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var userId = request.GetUserId();
            if (string.IsNullOrEmpty(userId))
            {
                _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: Rechazado: UserId vacío");
                return new RefreshFcmResult
                {
                    Success = false,
                    Message = "User ID is required"
                };
            }

            var user = await _unitOfWork.Users.GetByIdAsync(userId, cancellationToken);

            if (user == null)
            {
                _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: Rechazado: User not found (UserId={0})", userId);
                return new RefreshFcmResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            var fcm = request.GetFcm();
            if (string.IsNullOrEmpty(fcm))
            {
                _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: Rechazado: FCM token vacío (UserId={0})", userId);
                return new RefreshFcmResult
                {
                    Success = false,
                    Message = "FCM token is required"
                };
            }

            var fcmNuevo = fcm.Trim();
            var fcmAnterior = (user.Fcm ?? string.Empty).Trim();
            var esOtroDispositivo = !string.IsNullOrEmpty(fcmAnterior) && fcmAnterior != fcmNuevo;

            _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: UserId={0}, FCM_anterior={1}, FCM_nuevo={2}, esOtroDispositivo={3}",
                userId,
                string.IsNullOrEmpty(fcmAnterior) ? "(vacío)" : fcmAnterior[..Math.Min(20, fcmAnterior.Length)] + "...",
                fcmNuevo[..Math.Min(20, fcmNuevo.Length)] + "...",
                esOtroDispositivo);

            if (esOtroDispositivo)
            {
                _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: Enviando notificación 'otro dispositivo' al token anterior (UserId={0})", userId);
                try
                {
                    var clientIp = request.ClientIp;
                    if (!string.IsNullOrEmpty(clientIp) && clientIp.Contains(','))
                        clientIp = clientIp.Split(',')[0].Trim();
                    var geo = await _ipGeoService.GetGeoFromIpAsync(clientIp, cancellationToken);

                    var logoutData = new Dictionary<string, string>
                    {
                        { "type", "LOGOUT" },
                        { "userId", userId },
                        { "ip", string.IsNullOrEmpty(clientIp) ? "Unknown IP" : clientIp },
                        { "city", geo?.City ?? "Unknown city" },
                        { "country", geo?.Country ?? "Unknown country" }
                    };
                    await _firebaseNotification.SendNotificationToUserAsync(
                        fcmAnterior,
                        "Sesión nueva",
                        "Has iniciado sesión desde otro dispositivo",
                        logoutData);
                    _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: Notificación enviada OK al dispositivo anterior (UserId={0})", userId);
                }
                catch (Exception ex)
                {
                    _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: Error enviando notificación al dispositivo anterior: {0}. UserId={1}", ex.Message, userId);
                }
            }

            user.UpdateFcm(fcmNuevo);
            _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: FCM actualizado a nuevo dispositivo (UserId={0})", userId);

            await _unitOfWork.SaveChangesAsync(cancellationToken);

            return new RefreshFcmResult
            {
                Success = true,
                Message = "FCM token updated successfully"
            };
        }
        catch (Exception ex)
        {
            _logger.Debug("[OTRO_DISPOSITIVO] RefreshFCM :: Excepción: {0}", ex.Message);
            return new RefreshFcmResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
