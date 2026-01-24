namespace BetsTrading.Application.Interfaces;

public interface IIpGeoService
{
    /// <summary>
    /// Obtiene ciudad, región y país desde la IP del cliente (ip-api.com). Igual que legacy GetGeoLocationFromIp.
    /// </summary>
    Task<IpGeoResult?> GetGeoFromIpAsync(string? ip, CancellationToken cancellationToken = default);
}

public class IpGeoResult
{
    public string? City { get; set; }
    public string? RegionName { get; set; }
    public string? Country { get; set; }
    public string? ISP { get; set; }
}
