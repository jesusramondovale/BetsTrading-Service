using System.Net;
using System.Reflection;
using System.Text;

namespace BetsTrading.API.Health;

/// <summary>
/// Genera el HTML de la vista pública /info (requisitos de página de inicio OAuth / verificación Google).
/// </summary>
public static class AppInfoView
{
    private const string ResourceName = "BetsTrading.API.Health.AppInfoPage.html";
    private const string PlaceholderPrivacyUrl = "__PRIVACY_POLICY_URL__";
    private const string PlaceholderPrivacyUrlPlain = "__PRIVACY_POLICY_URL_PLAIN__";
    private const string PlaceholderPrivacyUrlEs = "__PRIVACY_POLICY_URL_ES__";
    private const string PlaceholderPrivacyUrlPlainEs = "__PRIVACY_POLICY_URL_PLAIN_ES__";

    /// <summary>
    /// URL por defecto (inglés). Debe coincidir con el enlace de la pantalla de consentimiento OAuth si esa es la política registrada.
    /// </summary>
    public const string DefaultPrivacyPolicyUrl =
        "https://raw.githubusercontent.com/jesusramondovale/BetsTrading-Client/refs/heads/android-master/policies/privacy_policy_en.md";

    /// <summary>
    /// URL por defecto de la política en español (mismo repositorio que el cliente).
    /// </summary>
    public const string DefaultPrivacyPolicyUrlEs =
        "https://raw.githubusercontent.com/jesusramondovale/BetsTrading-Client/refs/heads/android-master/policies/politica_privacidad_es.md";

    public static string GetHtml(string? privacyPolicyUrlEn, string? privacyPolicyUrlEs)
    {
        var en = string.IsNullOrWhiteSpace(privacyPolicyUrlEn) ? DefaultPrivacyPolicyUrl : privacyPolicyUrlEn.Trim();
        var es = string.IsNullOrWhiteSpace(privacyPolicyUrlEs) ? DefaultPrivacyPolicyUrlEs : privacyPolicyUrlEs.Trim();

        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso embebido no encontrado: {ResourceName}.");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        var template = reader.ReadToEnd();

        return template
            .Replace(PlaceholderPrivacyUrl, WebUtility.HtmlEncode(en))
            .Replace(PlaceholderPrivacyUrlPlain, WebUtility.HtmlEncode(en))
            .Replace(PlaceholderPrivacyUrlEs, WebUtility.HtmlEncode(es))
            .Replace(PlaceholderPrivacyUrlPlainEs, WebUtility.HtmlEncode(es));
    }
}
