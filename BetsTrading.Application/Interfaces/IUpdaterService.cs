namespace BetsTrading.Application.Interfaces;

public interface IUpdaterService
{
    Task UpdateAssetsAsync(bool marketHours, CancellationToken cancellationToken = default);
    Task CreateBetZonesAsync(bool marketHours, CancellationToken cancellationToken = default);
    Task CheckBetsAsync(bool marketHours, CancellationToken cancellationToken = default);
    Task RefreshTargetOddsAsync(CancellationToken cancellationToken = default);

    /// <summary>Rellena max odds en memoria desde las BetZones activas en BD. Llamar al arranque para que Trends tenga datos de inmediato.</summary>
    Task RefreshMaxOddsFromDatabaseAsync(CancellationToken cancellationToken = default);

    /// <summary>Obsoleto: los trends se calculan en la API desde max odds en memoria. No-op.</summary>
    [Obsolete("Trends are computed on-demand in the API from in-memory max odds.")]
    Task UpdateTrendsAsync(bool marketHours, CancellationToken cancellationToken = default);
}
