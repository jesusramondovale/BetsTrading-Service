using System.Collections.Concurrent;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.Services;

/// <summary>
/// Almacena overrides de configuración en memoria; modificable en tiempo real desde el panel admin.
/// </summary>
public sealed class AdminRuntimeConfig : IAdminRuntimeConfig
{
    private int? _updaterMinute;
    private int? _oddsAdjusterRefreshTimeSeconds;
    private int[]? _dailyRewardCoinsByDay;
    private double? _dailyRewardWindowToClaimHours;
    private double? _dailyRewardWindowUntilStreakLostHours;
    private readonly ConcurrentDictionary<string, string> _exchangeOptionsByCurrency = new();

    public int? UpdaterMinute => _updaterMinute;
    public int? OddsAdjusterRefreshTimeSeconds => _oddsAdjusterRefreshTimeSeconds;
    public int[]? DailyRewardCoinsByDay => _dailyRewardCoinsByDay;
    public double? DailyRewardWindowToClaimHours => _dailyRewardWindowToClaimHours;
    public double? DailyRewardWindowUntilStreakLostHours => _dailyRewardWindowUntilStreakLostHours;

    public string? ExchangeOptionsEur => _exchangeOptionsByCurrency.TryGetValue("eur", out var v) ? v : null;
    public string? ExchangeOptionsUsd => _exchangeOptionsByCurrency.TryGetValue("usd", out var v) ? v : null;

    public string? GetExchangeOptions(string currency)
    {
        var key = (currency ?? "eur").ToLowerInvariant();
        return _exchangeOptionsByCurrency.TryGetValue(key, out var v) ? v : null;
    }

    public void SetFromDto(AdminConfigDto dto)
    {
        _updaterMinute = dto.UpdaterMinute;
        _oddsAdjusterRefreshTimeSeconds = dto.OddsAdjusterRefreshTimeSeconds;
        _dailyRewardCoinsByDay = dto.DailyRewardCoinsByDay;
        _dailyRewardWindowToClaimHours = dto.DailyRewardWindowToClaimHours;
        _dailyRewardWindowUntilStreakLostHours = dto.DailyRewardWindowUntilStreakLostHours;
        if (dto.ExchangeOptionsEur != null)
            _exchangeOptionsByCurrency["eur"] = dto.ExchangeOptionsEur;
        if (dto.ExchangeOptionsUsd != null)
            _exchangeOptionsByCurrency["usd"] = dto.ExchangeOptionsUsd;
    }

    public AdminConfigDto ToDto(
        string? exchangeOptionsEurFromFile,
        string? exchangeOptionsUsdFromFile)
    {
        return new AdminConfigDto
        {
            UpdaterMinute = _updaterMinute ?? 15,
            OddsAdjusterRefreshTimeSeconds = _oddsAdjusterRefreshTimeSeconds ?? 4,
            DailyRewardCoinsByDay = _dailyRewardCoinsByDay ?? new[] { 5, 10, 15, 25, 40, 50 },
            DailyRewardWindowToClaimHours = _dailyRewardWindowToClaimHours ?? 24,
            DailyRewardWindowUntilStreakLostHours = _dailyRewardWindowUntilStreakLostHours ?? 48,
            ExchangeOptionsEur = ExchangeOptionsEur ?? exchangeOptionsEurFromFile ?? "[]",
            ExchangeOptionsUsd = ExchangeOptionsUsd ?? exchangeOptionsUsdFromFile ?? "[]",
        };
    }
}

/// <summary>DTO para GET/POST del panel admin.</summary>
public class AdminConfigDto
{
    public int UpdaterMinute { get; set; } = 15;
    public int OddsAdjusterRefreshTimeSeconds { get; set; } = 4;
    public int[] DailyRewardCoinsByDay { get; set; } = { 5, 10, 15, 25, 40, 50 };
    public double DailyRewardWindowToClaimHours { get; set; } = 24;
    public double DailyRewardWindowUntilStreakLostHours { get; set; } = 48;
    public string ExchangeOptionsEur { get; set; } = "[]";
    public string ExchangeOptionsUsd { get; set; } = "[]";
}
