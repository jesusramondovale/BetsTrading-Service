namespace BetsTrading.Application.Interfaces;

public interface IFirebaseNotificationService
{
    Task SendNotificationToUserAsync(string deviceToken, string title, string body, Dictionary<string, string>? additionalData = null);

    /// <summary>
    /// Envía LOGOUT por mantenimiento a cada token (mismo flujo que otro dispositivo en la app).
    /// </summary>
    Task<int> BroadcastMaintenanceLogoutAsync(
        IEnumerable<string> deviceTokens,
        CancellationToken cancellationToken = default);
}
