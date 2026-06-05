using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace BetsTrading.API.Security;

public static class DiditWebhookVerifier
{
    public static bool IsValid(string rawBody, IHeaderDictionary headers)
    {
        var secret = Environment.GetEnvironmentVariable("DIDIT_WEBHOOK_SECRET");
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        var timestampHeader = headers["X-Timestamp"].ToString();
        if (!string.IsNullOrWhiteSpace(timestampHeader) &&
            long.TryParse(timestampHeader, out var timestamp))
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - timestamp) > 300)
                return false;
        }

        var signatureV2 = headers["X-Signature-V2"].ToString();
        if (!string.IsNullOrWhiteSpace(signatureV2) &&
            TryVerifyCanonicalJson(rawBody, signatureV2, secret))
            return true;

        var signature = headers["X-Signature"].ToString();
        if (!string.IsNullOrWhiteSpace(signature) &&
            TryVerifyRawBody(rawBody, signature, secret))
            return true;

        var signatureSimple = headers["X-Signature-Simple"].ToString();
        if (!string.IsNullOrWhiteSpace(signatureSimple) &&
            !string.IsNullOrWhiteSpace(timestampHeader) &&
            TryParseEnvelope(rawBody, out var sessionId, out var status, out var webhookType))
        {
            var envelope = $"{timestampHeader}:{sessionId}:{status}:{webhookType}";
            return TryVerifyRawBody(envelope, signatureSimple, secret);
        }

        return false;
    }

    private static bool TryVerifyCanonicalJson(string rawBody, string providedSignature, string secret)
    {
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var canonical = Canonicalize(doc.RootElement);
            return TryVerifyRawBody(canonical, providedSignature, secret);
        }
        catch
        {
            return false;
        }
    }

    private static string Canonicalize(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
            return element.GetRawText();

        var props = element.EnumerateObject()
            .OrderBy(p => p.Name, StringComparer.Ordinal)
            .Select(p => $"\"{p.Name}\":{Canonicalize(p.Value)}");
        return "{" + string.Join(",", props) + "}";
    }

    private static bool TryVerifyRawBody(string content, string providedSignature, string secret)
    {
        var expected = ComputeHmacHex(secret, content);
        return FixedTimeEquals(expected, providedSignature.Trim());
    }

    private static string ComputeHmacHex(string secret, string content)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static bool FixedTimeEquals(string expected, string provided)
    {
        try
        {
            var expectedBytes = Convert.FromHexString(expected);
            var providedBytes = Convert.FromHexString(provided);
            return CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes);
        }
        catch
        {
            return false;
        }
    }

    private static bool TryParseEnvelope(
        string rawBody,
        out string sessionId,
        out string status,
        out string webhookType)
    {
        sessionId = "";
        status = "";
        webhookType = "";
        try
        {
            using var doc = JsonDocument.Parse(rawBody);
            var root = doc.RootElement;
            sessionId = root.TryGetProperty("session_id", out var s) ? s.GetString() ?? "" : "";
            status = root.TryGetProperty("status", out var st) ? st.GetString() ?? "" : "";
            webhookType = root.TryGetProperty("webhook_type", out var wt) ? wt.GetString() ?? "" : "";
            return true;
        }
        catch
        {
            return false;
        }
    }
}
