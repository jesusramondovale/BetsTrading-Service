using BetsTrading_Service.Locale;
using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace BetsTrading_Service.Services
{
  public class FirebaseNotificationService
  {
    public FirebaseNotificationService()
    {
      if (FirebaseApp.DefaultInstance == null)
      {
        FirebaseApp.Create(new AppOptions()
        {
          Credential = GoogleCredential.FromFile(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "betrader-v1-firebase.json"))
        });
      }
    }

    public async Task SendNotificationToUser(string deviceToken, string title, string body, Dictionary<string, string> additionalData = null!)
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
      }
    }

  }
}
