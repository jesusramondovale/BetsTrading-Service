using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Services;
using BetsTrading.Application.Interfaces;
using BCrypt.Net;

namespace BetsTrading.Application.Commands.Auth;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IJwtTokenService _jwtTokenService;
    private readonly IFirebaseNotificationService _firebaseNotification;
    private readonly IApplicationLogger _logger;
    private const int SESSION_EXP_DAYS = 15;

    private readonly IIpGeoService _ipGeoService;

    public LoginCommandHandler(IUnitOfWork unitOfWork, IJwtTokenService jwtTokenService,
        IFirebaseNotificationService firebaseNotification, IIpGeoService ipGeoService, IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _jwtTokenService = jwtTokenService;
        _firebaseNotification = firebaseNotification;
        _ipGeoService = ipGeoService;
        _logger = logger;
    }

    public async Task<LoginResult> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _unitOfWork.Users.GetByEmailOrUsernameAsync(request.Username, cancellationToken);
        
        if (user == null)
        {
            _logger.Warning("[AUTH] :: Login :: User not found: {0}", request.Username);
            return new LoginResult
            {
                Success = false,
                Message = "Invalid username or password"
            };
        }

        if (!BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
        {
            user.RecordFailedLoginAttempt();
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            _logger.Warning("[AUTH] :: Login :: Invalid password for user: {0}", request.Username);
            return new LoginResult
            {
                Success = false,
                Message = "Invalid username or password"
            };
        }

        // Update session
        user.UpdateSession();
        
        if (!string.IsNullOrEmpty(request.Fcm))
        {
            var fcmNuevo = request.Fcm.Trim();
            var fcmAnterior = (user.Fcm ?? string.Empty).Trim();
            var esOtroDispositivo = !string.IsNullOrEmpty(fcmAnterior) && fcmAnterior != fcmNuevo;

            _logger.Debug("[OTRO_DISPOSITIVO] Login :: UserId={0}, FCM_anterior={1}, FCM_nuevo={2}, esOtroDispositivo={3}",
                user.Id,
                string.IsNullOrEmpty(fcmAnterior) ? "(vacío)" : fcmAnterior[..Math.Min(20, fcmAnterior.Length)] + "...",
                fcmNuevo[..Math.Min(20, fcmNuevo.Length)] + "...",
                esOtroDispositivo);

            if (esOtroDispositivo)
            {
                _logger.Debug("[OTRO_DISPOSITIVO] Login :: Enviando notificación 'otro dispositivo' al token anterior (UserId={0})", user.Id);
                try
                {
                    var clientIp = request.ClientIp;
                    if (!string.IsNullOrEmpty(clientIp) && clientIp.Contains(','))
                        clientIp = clientIp.Split(',')[0].Trim();
                    var geo = await _ipGeoService.GetGeoFromIpAsync(clientIp, cancellationToken);

                    var logoutData = new Dictionary<string, string>
                    {
                        { "type", "LOGOUT" },
                        { "userId", user.Id },
                        { "ip", string.IsNullOrEmpty(clientIp) ? "Unknown IP" : clientIp },
                        { "city", geo?.City ?? "Unknown city" },
                        { "country", geo?.Country ?? "Unknown country" }
                    };
                    await _firebaseNotification.SendNotificationToUserAsync(
                        fcmAnterior,
                        "Sesión nueva",
                        "Has iniciado sesión desde otro dispositivo",
                        logoutData);
                    _logger.Debug("[OTRO_DISPOSITIVO] Login :: Notificación enviada OK al dispositivo anterior (UserId={0})", user.Id);
                }
                catch (Exception ex)
                {
                    _logger.Debug("[OTRO_DISPOSITIVO] Login :: Error enviando notificación al dispositivo anterior: {0}. UserId={1}", ex.Message, user.Id);
                }
            }

            user.UpdateFcm(fcmNuevo);
            _logger.Debug("[OTRO_DISPOSITIVO] Login :: FCM actualizado a nuevo dispositivo (UserId={0})", user.Id);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);

        // Generate JWT token
        var jwtToken = _jwtTokenService.GenerateToken(user.Id, user.Email, user.Fullname, 12);

        _logger.Information("[AUTH] :: Login :: Success for user: {0}", user.Id);

        return new LoginResult
        {
            Success = true,
            Message = "Login successful",
            UserId = user.Id,
            JwtToken = jwtToken
        };
    }
}
