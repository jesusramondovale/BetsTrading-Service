namespace BetsTrading.Domain.Entities;

public class AssetCandle
{
    public int AssetId { get; set; }
    public string? Exchange { get; set; }
    public string? Interval { get; set; }     // "1h", "1d"...
    public DateTime DateTime { get; set; }   // UTC
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
}

public class AssetCandleUSD
{
    public int AssetId { get; set; }
    public string? Exchange { get; set; }
    public string? Interval { get; set; }     // "1h", "1d"...
    public DateTime DateTime { get; set; }   // UTC
    public decimal Open { get; set; }
    public decimal High { get; set; }
    public decimal Low { get; set; }
    public decimal Close { get; set; }
}
