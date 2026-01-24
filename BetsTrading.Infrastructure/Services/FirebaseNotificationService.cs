using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.Services;

public class FirebaseSettings
{
    public string CredentialsPath { get; set; } = "betrader-v1-firebase.json";
}

public class FirebaseNotificationService : IFirebaseNotificationService
{
    private readonly FirebaseSettings _settings;
    private readonly IApplicationLogger _logger;
    private readonly bool _firebaseAvailable;

    public FirebaseNotificationService(FirebaseSettings settings, IApplicationLogger logger)
    {
        _settings = settings;
        _logger = logger;
        _firebaseAvailable = false;

        if (FirebaseApp.DefaultInstance == null)
        {
            var credentialsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settings.CredentialsPath);

            if (!File.Exists(credentialsPath))
            {
                _logger.Debug("[OTRO_DISPOSITIVO] Firebase :: Credentials no encontradas en {0}. Notificaciones 'otro dispositivo' deshabilitadas.", credentialsPath);
                return;
            }

            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromFile(credentialsPath)
            });
            _firebaseAvailable = true;
            _logger.Debug("[OTRO_DISPOSITIVO] Firebase :: Inicializado OK. Credentials={0}", credentialsPath);
        }
        else
        {
            _firebaseAvailable = true;
        }
    }

    public async Task SendNotificationToUserAsync(string deviceToken, string title, string body, Dictionary<string, string>? additionalData = null)
    {
        _logger.Debug("[OTRO_DISPOSITIVO] Firebase :: Inicio envío. Token={0}..., Title={1}, Body={2}, type={3}, FirebaseAvailable={4}",
            deviceToken.Length > 20 ? deviceToken[..20] + "..." : deviceToken,
            title,
            body,
            additionalData != null && additionalData.TryGetValue("type", out var t) ? t : "(sin type)",
            _firebaseAvailable);

        if (!_firebaseAvailable)
        {
            _logger.Debug("[OTRO_DISPOSITIVO] Firebase :: Credentials no configuradas, omitiendo envío.");
            await Task.CompletedTask;
            return;
        }

        var data = new Dictionary<string, string>
        {
            { "id", Guid.NewGuid().ToString() }  // Random ID to avoid stacking notifications
        };

        if (additionalData != null)
        {
            foreach (var entry in additionalData)
            {
                data.Add(entry.Key, entry.Value);
            }
        }

        var message = new Message()
        {
            Token = deviceToken,
            Notification = new Notification
            {
                Title = title,
                Body = body,
            },
            Data = data,
            Android = new AndroidConfig
            {
                Priority = Priority.High,
                TimeToLive = TimeSpan.FromSeconds(60),
            },
        };

        try
        {
            _logger.Debug("[OTRO_DISPOSITIVO] Firebase :: Llamando FirebaseMessaging.SendAsync");
            string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            _logger.Debug("[OTRO_DISPOSITIVO] Firebase :: Mensaje enviado OK. Response={0}", response);
        }
        catch (Exception ex)
        {
            _logger.Debug("[OTRO_DISPOSITIVO] Firebase :: Error enviando mensaje: {0}", ex.Message);
            throw; // Re-throw para que el logger pueda capturarlo
        }
    }
}
