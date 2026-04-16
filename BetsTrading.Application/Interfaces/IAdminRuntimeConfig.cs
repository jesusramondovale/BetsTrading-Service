using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Interfaces;

/// <summary>
/// Configuración del servidor que puede modificarse en tiempo real desde el panel admin de /status.
/// Los valores devueltos son overrides; si son null, el consumidor debe usar el valor por defecto.
/// </summary>
public interface IAdminRuntimeConfig
{
    /// <summary>Minuto UTC en que corre el updater de assets (ej. 15 = XX:15). Null = usar 15.</summary>
    int? UpdaterMinute { get; }

    /// <summary>Intervalo en segundos entre refrescos de odds. Null = usar valor por defecto (4).</summary>
    int? OddsAdjusterRefreshTimeSeconds { get; }

    /// <summary>Monedas por día de recompensa [día1, día2, ...]. Null = usar [5,10,15,25,40,50].</summary>
    int[]? DailyRewardCoinsByDay { get; }

    /// <summary>Horas entre claims para no perder ventana (24). Null = 24.</summary>
    double? DailyRewardWindowToClaimHours { get; }

    /// <summary>Horas sin claim para perder racha (48). Null = 48.</summary>
    double? DailyRewardWindowUntilStreakLostHours { get; }

    /// <summary>JSON de opciones de exchange para EUR. Null = leer de archivo.</summary>
    string? ExchangeOptionsEur { get; }

    /// <summary>JSON de opciones de exchange para USD. Null = leer de archivo.</summary>
    string? ExchangeOptionsUsd { get; }

    /// <summary>JSON de opciones de exchange para la moneda indicada (ej. eur, usd). Null = leer de archivo.</summary>
    string? GetExchangeOptions(string currency);

    /// <summary>Horas de caducidad del JWT emitido por la API (login, registro, GoogleLogIn). Null = 96 (4 días).</summary>
    int? JwtTokenExpirationHours { get; }

    /// <summary>
    /// Ids de FinancialAsset para crear favoritos en cada registro (Google o estándar).
    /// Null = nunca guardado desde el panel (el registro usa 97 y 87).
    /// Array vacío = no crear favoritos. Si hay elementos, se usan en orden (duplicados filtrados en servidor).
    /// </summary>
    int[]? RegistrationDefaultFavoriteAssetIds { get; }

    /// <summary>Minutos en primer plano (lado app) antes de intentar intersticial. Null = 10.</summary>
    int? MandatoryAdForegroundMinutes { get; }

    /// <summary>Segundos mínimos entre dos intersticiales obligatorios. Null = 300.</summary>
    int? MandatoryAdCooldownSeconds { get; }

    /// <summary>Precio Stripe compra No Ads en EUR (minor units se calcula ×100). Null = 4.99.</summary>
    double? NoAdsPriceEur { get; }

    /// <summary>Precio Stripe compra No Ads en USD. Null = 4.99.</summary>
    double? NoAdsPriceUsd { get; }

    /// <summary>Snapshot con defaults aplicados para el cliente móvil.</summary>
    PublicClientAdsConfig GetPublicClientAdsConfig(string? exchangeOptionsEurFromFile, string? exchangeOptionsUsdFromFile);
}
