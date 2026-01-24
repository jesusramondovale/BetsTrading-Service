using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BetsTrading.Application.Services;

public class AdMobSsvVerifier
{
    private const string VerifierKeysUrl = "https://www.gstatic.com/admob/reward/verifier-keys.json";

    public static async Task<bool> VerifySignatureAsync(string queryString, string signatureB64u, string keyIdText, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(signatureB64u) || string.IsNullOrEmpty(keyIdText))
            return false;

        if (!ulong.TryParse(keyIdText, out var keyId))
            return false;

        // Remove signature from query string for verification
        var query = queryString.TrimStart('?');
        var iSig = query.IndexOf("signature=", StringComparison.Ordinal);
        if (iSig < 0) return false;
        
        var toVerify = query[..(iSig - 1)];
        var data = Encoding.UTF8.GetBytes(toVerify);

        // Get public key from AdMob
        using var http = new HttpClient();
        HttpResponseMessage response;
        try
        {
            response = await http.GetAsync(VerifierKeysUrl, cancellationToken);
        }
        catch (Exception)
        {
            // Network error - cannot verify signature
            return false;
        }

        if (!response.IsSuccessStatusCode)
        {
            // HTTP error (e.g., 521 from Cloudflare) - cannot verify signature
            return false;
        }

        string json;
        try
        {
            json = await response.Content.ReadAsStringAsync(cancellationToken);
        }
        catch (Exception)
        {
            // Error reading response - cannot verify signature
            return false;
        }

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            // Invalid JSON response (e.g., HTML error page from Cloudflare) - cannot verify signature
            return false;
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("keys", out var keysElement))
            {
                return false;
            }

            var keys = keysElement;

        string? pem = null;
        foreach (var k in keys.EnumerateArray())
        {
            if (k.TryGetProperty("keyId", out var kid) &&
                (kid.TryGetUInt64(out var kidU) ? kidU == keyId
                 : kid.ValueKind == JsonValueKind.String && kid.GetString() == keyIdText))
            {
                pem = k.GetProperty("pem").GetString();
                break;
            }
        }

            if (string.IsNullOrEmpty(pem))
                return false;

            // Verify signature
            using var ecdsa = ECDsa.Create();
            try
            {
                ecdsa.ImportFromPem(pem);
            }
            catch
            {
                return false;
            }

            byte[] sig;
            try
            {
                sig = Base64UrlDecode(signatureB64u);
            }
            catch
            {
                return false;
            }

            return ecdsa.VerifyData(data, sig, HashAlgorithmName.SHA256, DSASignatureFormat.Rfc3279DerSequence);
        }
    }

    private static byte[] Base64UrlDecode(string input)
    {
        var base64 = input.Replace('-', '+').Replace('_', '/');
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
