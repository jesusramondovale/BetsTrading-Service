namespace BetsTrading.Application.Interfaces;

/// <summary>
/// Servicio en memoria que mantiene el máximo odd por ticker y timeframe (1, 2, 4, 24 horas)
/// para cada moneda (EUR y USD). Actualizado por UpdaterService cuando cambian target_odds.
/// Incluye ZoneId para que el cliente pueda abrir la confirmación de apuesta de esa zona.
/// </summary>
public interface ITickerMaxOddsService
{
    /// <summary>Estructura: por ticker, por timeframe (1,2,4,24), (MaxOdd, Direction, ZoneId).</summary>
    void SetMaxOddsEur(string ticker, int timeframe, double maxOdd, int direction, int zoneId);

    /// <summary>Estructura: por ticker, por timeframe (1,2,4,24), (MaxOdd, Direction, ZoneId).</summary>
    void SetMaxOddsUsd(string ticker, int timeframe, double maxOdd, int direction, int zoneId);

    /// <summary>Reemplaza todos los datos EUR en una pasada (thread-safe).</summary>
    void ReplaceAllEur(IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>> data);

    /// <summary>Reemplaza todos los datos USD en una pasada (thread-safe).</summary>
    void ReplaceAllUsd(IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>> data);

    /// <summary>Máximo odd para un ticker en la moneda dada (máximo sobre los 4 timeframes). Devuelve null si no hay datos. ZoneId y Timeframe del mejor.</summary>
    (double MaxOdd, int Direction, int ZoneId, int Timeframe)? GetMaxOddForTicker(string currency, string ticker);

    /// <summary>Max odds por timeframe (1, 2, 4, 24) para un ticker. Solo incluye timeframes con datos.</summary>
    IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)> GetMaxOddsByTimeframe(string currency, string ticker);

    /// <summary>Todos los tickers con sus max odds por timeframe para la moneda. Para exponer en API.</summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction, int ZoneId)>> GetAllMaxOdds(string currency);

    /// <summary>Top N tickers ordenados por max odd descendente para la moneda. Para la API Trends. Incluye ZoneId y Timeframe del mejor.</summary>
    IReadOnlyList<(string Ticker, double MaxOdd, int Direction, int ZoneId, int Timeframe)> GetTopTickersByMaxOdd(string currency, int count);
}
