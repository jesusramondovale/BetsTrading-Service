using System.Reflection;
using System.Text;

namespace BetsTrading.API.Health;

/// <summary>
/// Genera el HTML de la vista /status a partir de la plantilla embebida StatusPage.html.
/// </summary>
public static class StatusView
{
    private const string ResourceName = "BetsTrading.API.Health.StatusPage.html";
    private const string Placeholder = "__SERVER_TIME_ISO__";

    /// <summary>
    /// Devuelve el HTML de la página de status, inyectando la hora UTC del servidor para los relojes.
    /// </summary>
    public static string GetHtml(string serverTimeIso)
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso embebido no encontrado: {ResourceName}. Comprueba que StatusPage.html está como EmbeddedResource.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var template = reader.ReadToEnd();

        var safe = serverTimeIso
            .Replace("\\", "\\\\")
            .Replace("\"", "\\\"");

        return template.Replace(Placeholder, safe);
    }
}
