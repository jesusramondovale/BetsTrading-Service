namespace BetsTrading.Application.Interfaces;

/// <summary>
/// Servicio en memoria que mantiene el máximo odd por ticker y timeframe (1, 2, 4, 24 horas)
/// para cada moneda (EUR y USD). Actualizado por UpdaterService cuando cambian target_odds.
/// </summary>
public interface ITickerMaxOddsService
{
    /// <summary>Estructura: por ticker, por timeframe (1,2,4,24), (MaxOdd, Direction).</summary>
    void SetMaxOddsEur(string ticker, int timeframe, double maxOdd, int direction);

    /// <summary>Estructura: por ticker, por timeframe (1,2,4,24), (MaxOdd, Direction).</summary>
    void SetMaxOddsUsd(string ticker, int timeframe, double maxOdd, int direction);

    /// <summary>Reemplaza todos los datos EUR en una pasada (thread-safe).</summary>
    void ReplaceAllEur(IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction)>> data);

    /// <summary>Reemplaza todos los datos USD en una pasada (thread-safe).</summary>
    void ReplaceAllUsd(IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction)>> data);

    /// <summary>Máximo odd para un ticker en la moneda dada (máximo sobre los 4 timeframes). Devuelve null si no hay datos.</summary>
    (double MaxOdd, int Direction)? GetMaxOddForTicker(string currency, string ticker);

    /// <summary>Max odds por timeframe (1, 2, 4, 24) para un ticker. Solo incluye timeframes con datos.</summary>
    IReadOnlyDictionary<int, (double MaxOdd, int Direction)> GetMaxOddsByTimeframe(string currency, string ticker);

    /// <summary>Todos los tickers con sus max odds por timeframe para la moneda. Para exponer en API.</summary>
    IReadOnlyDictionary<string, IReadOnlyDictionary<int, (double MaxOdd, int Direction)>> GetAllMaxOdds(string currency);

    /// <summary>Top N tickers ordenados por max odd descendente para la moneda. Para la API Trends.</summary>
    IReadOnlyList<(string Ticker, double MaxOdd, int Direction)> GetTopTickersByMaxOdd(string currency, int count);
}
