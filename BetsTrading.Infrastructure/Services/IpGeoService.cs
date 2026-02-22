using System.Text.Json;
using BetsTrading.Application.Interfaces;
using Microsoft.Extensions.Http;

namespace BetsTrading.Infrastructure.Services;

/// <summary>
/// Geolocalización por IP vía ip-api.com. Mismo comportamiento que legacy AuthController.GetGeoLocationFromIp.
/// </summary>
public class IpGeoService : IIpGeoService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public IpGeoService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public async Task<IpGeoResult?> GetGeoFromIpAsync(string? ip, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ip))
            return null;

        try
        {
            var http = _httpClientFactory.CreateClient("IpGeo");
            var response = await http.GetAsync($"http://ip-api.com/json/{ip}", cancellationToken);

            if (!response.IsSuccessStatusCode)
                return null;

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            var raw = JsonSerializer.Deserialize<IpApiResponse>(json, JsonOptions);

            if (raw == null || raw.Status != "success")
                return null;

            return new IpGeoResult
            {
                City = raw.City,
                RegionName = raw.RegionName,
                Country = raw.Country,
                ISP = raw.ISP
            };
        }
        catch
        {
            return null;
        }
    }

    private class IpApiResponse
    {
        public string? Status { get; set; }
        public string? City { get; set; }
        public string? RegionName { get; set; }
        public string? Country { get; set; }
        public string? ISP { get; set; }
    }
}
