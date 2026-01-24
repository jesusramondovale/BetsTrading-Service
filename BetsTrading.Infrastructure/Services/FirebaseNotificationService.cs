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

    public FirebaseNotificationService(FirebaseSettings settings)
    {
        _settings = settings;
        
        // Inicializar Firebase Admin SDK si no está inicializado
        if (FirebaseApp.DefaultInstance == null)
        {
            var credentialsPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, _settings.CredentialsPath);
            
            if (!File.Exists(credentialsPath))
            {
                throw new FileNotFoundException($"Firebase credentials file not found at: {credentialsPath}");
            }

            FirebaseApp.Create(new AppOptions()
            {
                Credential = GoogleCredential.FromFile(credentialsPath)
            });
        }
    }

    public async Task SendNotificationToUserAsync(string deviceToken, string title, string body, Dictionary<string, string>? additionalData = null)
    {
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
            Token = deviceToken,  // Firebase Cloud Messaging Token (User token)
            Notification = new Notification
            {
                Title = title,
                Body = body,
            },
            Data = data
        };

        try
        {
            string response = await FirebaseMessaging.DefaultInstance.SendAsync(message);
            Console.WriteLine("Message sent successfully: " + response);
        }
        catch (Exception ex)
        {
            Console.WriteLine("Error sending message: " + ex.Message);
            throw; // Re-throw para que el logger pueda capturarlo
        }
    }
}
