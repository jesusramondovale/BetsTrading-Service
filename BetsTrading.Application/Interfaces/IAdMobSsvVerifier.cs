namespace BetsTrading.Application.Interfaces;

/// <summary>
/// Verifica la firma SSV de recompensas de AdMob.
/// </summary>
public interface IAdMobSsvVerifier
{
    Task<bool> VerifySignatureAsync(string queryString, string signatureB64u, string keyIdText, CancellationToken cancellationToken = default);
}
