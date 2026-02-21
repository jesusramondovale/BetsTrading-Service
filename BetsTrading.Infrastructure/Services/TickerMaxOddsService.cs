using System.Collections.Concurrent;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.Services;

/// <summary>
/// Mantiene en memoria (ticker, timeframe) -> (MaxOdd, Direction, ZoneId) para EUR y USD.
/// Actualizado por UpdaterService; consumido por API Trends y por endpoint MaxOdds.
/// </summary>
public sealed class TickerMaxOddsService : ITickerMaxOddsService
{
    private readonly object _eurLock = new();
    private readonly object _usdLock = new();
    private Dictionary<string, Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>> _eur =
        new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>> _usd =
        new(StringComparer.OrdinalIgnoreCase);

    public void SetMaxOddsEur(string ticker, int timeframe, double maxOdd, int direction, int zoneId)
    {
        lock (_eurLock)
        {
            if (!_eur.TryGetValue(ticker, out var byTf))
            {
                byTf = new Dictionary<int, (double, int, int)>();
                _eur[ticker] = byTf;
            }
            byTf[timeframe] = (maxOdd, direction, zoneId);
        }
    }

    public void SetMaxOddsUsd(string ticker, int timeframe, double maxOdd, int direction, int zoneId)
    {
        lock (_usdLock)
        {
            if (!_usd.TryGetValue(ticker, out var byTf))
            {
                byTf = new Dictionary<int, (double, int, int)>();
                _usd[ticker] = byTf;
            }
            byTf[timeframe] = (maxOdd, direction, zoneId);
        }
    }

    public void ReplaceAllEur(IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>> data)
    {
        var copy = new Dictionary<string, Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in data)
        {
            copy[kv.Key] = new Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>(kv.Value);
        }
        lock (_eurLock)
        {
            _eur = copy;
        }
    }

    public void ReplaceAllUsd(IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>> data)
    {
        var copy = new Dictionary<string, Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>>(StringComparer.OrdinalIgnoreCase);
        foreach (var kv in data)
        {
            copy[kv.Key] = new Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>(kv.Value);
        }
        lock (_usdLock)
        {
            _usd = copy;
        }
    }

    public (double MaxOdd, int Direction, int ZoneId, int Timeframe)? GetMaxOddForTicker(string currency, string ticker)
    {
        var isEur = string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase);
        if (isEur)
        {
            lock (_eurLock)
            {
                if (_eur.TryGetValue(ticker, out var byTf) && byTf.Count > 0)
                {
                    var best = byTf.OrderByDescending(x => x.Value.MaxOdd).First();
                    return (best.Value.MaxOdd, best.Value.Direction, best.Value.ZoneId, best.Key);
                }
            }
        }
        else
        {
            lock (_usdLock)
            {
                if (_usd.TryGetValue(ticker, out var byTf) && byTf.Count > 0)
                {
                    var best = byTf.OrderByDescending(x => x.Value.MaxOdd).First();
                    return (best.Value.MaxOdd, best.Value.Direction, best.Value.ZoneId, best.Key);
                }
            }
        }
        return null;
    }

    public IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)> GetMaxOddsByTimeframe(string currency, string ticker)
    {
        var isEur = string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase);
        lock (isEur ? _eurLock : _usdLock)
        {
            var dict = isEur ? _eur : _usd;
            if (dict.TryGetValue(ticker, out var byTf))
                return new Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>(byTf);
        }
        return new Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>();
    }

    public IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>> GetAllMaxOdds(string currency)
    {
        var isEur = string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase);
        lock (isEur ? _eurLock : _usdLock)
        {
            var dict = isEur ? _eur : _usd;
            return dict.ToDictionary(
                kv => kv.Key,
                kv => (IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>)new Dictionary<int, (double MaxOdd, int Direction, int ZoneId)>(kv.Value),
                StringComparer.OrdinalIgnoreCase);
        }
    }

    public IReadOnlyList<(string Ticker, double MaxOdd, int Direction, int ZoneId, int Timeframe)> GetTopTickersByMaxOdd(string currency, int count)
    {
        var isEur = string.Equals(currency, "EUR", StringComparison.OrdinalIgnoreCase);
        lock (isEur ? _eurLock : _usdLock)
        {
            var dict = isEur ? _eur : _usd;
            return dict
                .Select(kv =>
                {
                    var best = kv.Value.OrderByDescending(x => x.Value.MaxOdd).First();
                    return (kv.Key, best.Value.MaxOdd, best.Value.Direction, best.Value.ZoneId, best.Key);
                })
                .OrderByDescending(x => x.MaxOdd)
                .Take(count)
                .ToList();
        }
    }
}
