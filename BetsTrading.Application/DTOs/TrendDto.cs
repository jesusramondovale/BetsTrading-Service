namespace BetsTrading.Application.DTOs;

public class TrendDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public double DailyGain { get; set; }
    public double Close { get; set; }
    public double Current { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public double? CurrentMaxOdd { get; set; }
    public int? CurrentMaxOddDirection { get; set; }
}
