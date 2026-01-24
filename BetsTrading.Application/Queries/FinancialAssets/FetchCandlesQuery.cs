using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.FinancialAssets;

public class FetchCandlesQuery : IRequest<FetchCandlesResult>
{
    /// <summary>Ticker del activo (ej. BTC, AAPL). Compatible con "id" del legacy symbolWithTimeframe.</summary>
    public string Ticker { get; set; } = string.Empty;

    /// <summary>Alternativa legacy: mismo valor que Ticker (symbolWithTimeframe.id).</summary>
    public string? Id { get; set; }

    public int? Timeframe { get; set; } = 1;

    public string Currency { get; set; } = "EUR";
}

public class FetchCandlesResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<CandleDto> Candles { get; set; } = new();
}
