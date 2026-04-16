namespace BetsTrading.Application.DTOs;

/// <summary>Configuración pública para la app (anuncios obligatorios y precio compra No Ads).</summary>
public sealed class PublicClientAdsConfig
{
    /// <summary>Minutos acumulados en primer plano (app) antes de intentar un intersticial.</summary>
    public int MandatoryAdForegroundMinutes { get; init; }

    public int MandatoryAdCooldownSeconds { get; init; }
    public double NoAdsPriceEur { get; init; }
    public double NoAdsPriceUsd { get; init; }
}
