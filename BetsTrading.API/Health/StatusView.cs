using System.Reflection;
using System.Text;

namespace BetsTrading.API.Health;

/// <summary>
/// Genera el HTML de la vista /status a partir de la plantilla embebida StatusPage.html.
/// </summary>
public static class StatusView
{
    private const string ResourceName = "BetsTrading.API.Health.StatusPage.html";
    private const string PlaceholderTime = "__SERVER_TIME_ISO__";
    private const string PlaceholderAdminHash = "__ADMIN_SECRET_HASH__";

    /// <summary>
    /// Devuelve el HTML de la página de status, inyectando la hora UTC y el hash del secret para el panel admin.
    /// </summary>
    /// <param name="serverTimeIso">Hora UTC del servidor en formato ISO.</param>
    /// <param name="adminSecretHash">SHA-256 del ADMIN_SECRET en hex (minúsculas), o vacío si no hay secret.</param>
    public static string GetHtml(string serverTimeIso, string adminSecretHash = "")
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso embebido no encontrado: {ResourceName}. Comprueba que StatusPage.html está como EmbeddedResource.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var template = reader.ReadToEnd();

        var safeTime = serverTimeIso
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");
        var safeHash = (adminSecretHash ?? "")
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");

        return template
            .Replace(PlaceholderTime, safeTime)
            .Replace(PlaceholderAdminHash, safeHash);
    }
}
