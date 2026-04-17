using System.Collections.Concurrent;
using System.Threading;
using BetsTrading.Application.DTOs;
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
    private int? _jwtTokenExpirationHours;
    private int[]? _registrationDefaultFavoriteAssetIds;
    private int? _mandatoryAdForegroundMinutes;
    private int? _mandatoryAdCooldownSeconds;
    private double? _noAdsPriceEur;
    private double? _noAdsPriceUsd;
    private long _configVersion = 0;

    public long ConfigVersion => Interlocked.Read(ref _configVersion);

    public int? UpdaterMinute => _updaterMinute;
    public int? OddsAdjusterRefreshTimeSeconds => _oddsAdjusterRefreshTimeSeconds;
    public int[]? DailyRewardCoinsByDay => _dailyRewardCoinsByDay;
    public double? DailyRewardWindowToClaimHours => _dailyRewardWindowToClaimHours;
    public double? DailyRewardWindowUntilStreakLostHours => _dailyRewardWindowUntilStreakLostHours;

    public string? ExchangeOptionsEur => _exchangeOptionsByCurrency.TryGetValue("eur", out var v) ? v : null;
    public string? ExchangeOptionsUsd => _exchangeOptionsByCurrency.TryGetValue("usd", out var v) ? v : null;

    public int? JwtTokenExpirationHours => _jwtTokenExpirationHours;

    public int[]? RegistrationDefaultFavoriteAssetIds => _registrationDefaultFavoriteAssetIds;

    public int? MandatoryAdForegroundMinutes => _mandatoryAdForegroundMinutes;

    public int? MandatoryAdCooldownSeconds => _mandatoryAdCooldownSeconds;

    public double? NoAdsPriceEur => _noAdsPriceEur;

    public double? NoAdsPriceUsd => _noAdsPriceUsd;

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
        if (dto.JwtTokenExpirationHours >= 1 && dto.JwtTokenExpirationHours <= 720)
            _jwtTokenExpirationHours = dto.JwtTokenExpirationHours;
        var favIds = dto.RegistrationDefaultFavoriteAssetIds;
        _registrationDefaultFavoriteAssetIds = (favIds ?? new[] { 97, 87 })
            .Where(id => id > 0)
            .Distinct()
            .ToArray();
        if (dto.ExchangeOptionsEur != null)
            _exchangeOptionsByCurrency["eur"] = dto.ExchangeOptionsEur;
        if (dto.ExchangeOptionsUsd != null)
            _exchangeOptionsByCurrency["usd"] = dto.ExchangeOptionsUsd;
        _mandatoryAdForegroundMinutes = dto.MandatoryAdForegroundMinutes;
        _mandatoryAdCooldownSeconds = dto.MandatoryAdCooldownSeconds;
        _noAdsPriceEur = dto.NoAdsPriceEur;
        _noAdsPriceUsd = dto.NoAdsPriceUsd;
        Interlocked.Increment(ref _configVersion);
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
            JwtTokenExpirationHours = _jwtTokenExpirationHours ?? 96,
            RegistrationDefaultFavoriteAssetIds = _registrationDefaultFavoriteAssetIds ?? new[] { 97, 87 },
            ExchangeOptionsEur = ExchangeOptionsEur ?? exchangeOptionsEurFromFile ?? "[]",
            ExchangeOptionsUsd = ExchangeOptionsUsd ?? exchangeOptionsUsdFromFile ?? "[]",
            MandatoryAdForegroundMinutes = _mandatoryAdForegroundMinutes ?? 10,
            MandatoryAdCooldownSeconds = _mandatoryAdCooldownSeconds ?? 300,
            NoAdsPriceEur = _noAdsPriceEur ?? 4.99,
            NoAdsPriceUsd = _noAdsPriceUsd ?? 4.99,
        };
    }

    public PublicClientAdsConfig GetPublicClientAdsConfig(string? exchangeOptionsEurFromFile, string? exchangeOptionsUsdFromFile)
    {
        var d = ToDto(exchangeOptionsEurFromFile, exchangeOptionsUsdFromFile);
        return new PublicClientAdsConfig
        {
            MandatoryAdForegroundMinutes = d.MandatoryAdForegroundMinutes,
            MandatoryAdCooldownSeconds = d.MandatoryAdCooldownSeconds,
            NoAdsPriceEur = d.NoAdsPriceEur,
            NoAdsPriceUsd = d.NoAdsPriceUsd,
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
    /// <summary>Horas hasta expiración del JWT de la API (1–720).</summary>
    public int JwtTokenExpirationHours { get; set; } = 96;
    /// <summary>Ids de activos para favoritos al registrar (lista dinámica; ej. 97,87 o siete ids).</summary>
    public int[] RegistrationDefaultFavoriteAssetIds { get; set; } = { 97, 87 };
    public string ExchangeOptionsEur { get; set; } = "[]";
    public string ExchangeOptionsUsd { get; set; } = "[]";

    /// <summary>Minutos en primer plano (cliente) antes de disparar intersticial por tiempo de uso.</summary>
    public int MandatoryAdForegroundMinutes { get; set; } = 10;

    /// <summary>Cooldown mínimo entre intersticiales obligatorios (segundos).</summary>
    public int MandatoryAdCooldownSeconds { get; set; } = 300;

    public double NoAdsPriceEur { get; set; } = 4.99;

    public double NoAdsPriceUsd { get; set; } = 4.99;
}
