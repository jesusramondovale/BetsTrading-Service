using System.Reflection;
using System.Text;

namespace BetsTrading.API.Health;

public static class DeleteAccountView
{
    private const string ResourceName = "BetsTrading.API.Health.DeleteAccountPage.html";

    public static string GetHtml()
    {
        var assembly = Assembly.GetExecutingAssembly();
        using var stream = assembly.GetManifestResourceStream(ResourceName)
            ?? throw new InvalidOperationException($"Recurso embebido no encontrado: {ResourceName}");

        using var reader = new StreamReader(stream, Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
