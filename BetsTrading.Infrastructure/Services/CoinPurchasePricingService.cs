using System.Text.Json;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.Services;

public class CoinPurchasePricingService : ICoinPurchasePricingService
{
    private readonly IAdminRuntimeConfig _adminConfig;

    public CoinPurchasePricingService(IAdminRuntimeConfig adminConfig)
    {
        _adminConfig = adminConfig;
    }

    public bool TryResolvePackage(string currency, int coins, long amountMinor, out CoinPurchasePackage package)
    {
        package = null!;
        var normalizedCurrency = (currency ?? "eur").Trim().ToLowerInvariant();
        var packages = LoadPackages(normalizedCurrency);
        package = packages.FirstOrDefault(p => p.Coins == coins) ?? null!;
        return package != null;
    }

    private IReadOnlyList<CoinPurchasePackage> LoadPackages(string currency)
    {
        var json = _adminConfig.GetExchangeOptions(currency);
        if (string.IsNullOrWhiteSpace(json))
        {
            var fileName = $"exchange_options_{currency}.json";
            var paths = new[]
            {
                Path.Combine(AppContext.BaseDirectory ?? "", fileName),
                Path.Combine(Directory.GetCurrentDirectory(), fileName),
            };

            foreach (var path in paths)
            {
                if (!File.Exists(path))
                    continue;
                try
                {
                    json = File.ReadAllText(path);
                    break;
                }
                catch
                {
                    return [];
                }
            }
        }

        if (string.IsNullOrWhiteSpace(json))
            return [];

        try
        {
            var options = JsonSerializer.Deserialize<List<ExchangeOptionDto>>(json,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
            return options
                .Where(o => string.Equals(o.Type, "buy", StringComparison.OrdinalIgnoreCase))
                .Select(o => new CoinPurchasePackage
                {
                    Coins = o.Coins,
                    Price = o.Euros,
                    Currency = currency
                })
                .ToList();
        }
        catch
        {
            return [];
        }
    }

    private sealed class ExchangeOptionDto
    {
        public int Coins { get; set; }
        public double Euros { get; set; }
        public string Type { get; set; } = string.Empty;
    }
}
