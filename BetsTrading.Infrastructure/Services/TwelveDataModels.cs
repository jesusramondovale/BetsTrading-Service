using System.Text.Json.Serialization;

namespace BetsTrading.Infrastructure.Services;

public sealed class TwelveDataResponse
{
    [JsonPropertyName("meta")]
    public TwelveMeta? Meta { get; set; }

    [JsonPropertyName("values")]
    public List<TwelveBar> Values { get; set; } = new();

    [JsonPropertyName("status")]
    public string? Status { get; set; }  // "ok" o "error"

    [JsonPropertyName("code")]
    public object? Code { get; set; }

    [JsonPropertyName("message")]
    public object? Message { get; set; }
}

public sealed class TwelveMeta
{
    [JsonPropertyName("symbol")]
    public string? Symbol { get; set; }

    [JsonPropertyName("interval")]
    public string? Interval { get; set; }

    [JsonPropertyName("currency_base")]
    public string? CurrencyBase { get; set; }

    [JsonPropertyName("currency_quote")]
    public string? CurrencyQuote { get; set; }

    [JsonPropertyName("exchange")]
    public string? Exchange { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }
}

public sealed class TwelveBar
{
    [JsonPropertyName("datetime")]
    public string? Datetime { get; set; }

    [JsonPropertyName("open")]
    public string? Open { get; set; }

    [JsonPropertyName("high")]
    public string? High { get; set; }

    [JsonPropertyName("low")]
    public string? Low { get; set; }

    [JsonPropertyName("close")]
    public string? Close { get; set; }
}
