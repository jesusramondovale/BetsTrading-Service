namespace BetsTrading.Application.Interfaces;

public interface IFirebaseNotificationService
{
    Task SendNotificationToUserAsync(string deviceToken, string title, string body, Dictionary<string, string>? additionalData = null);
}
