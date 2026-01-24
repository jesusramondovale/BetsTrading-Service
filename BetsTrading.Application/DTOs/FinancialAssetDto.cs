namespace BetsTrading.Application.DTOs;

public class FinancialAssetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Country { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public double CurrentEur { get; set; }
    public double CurrentUsd { get; set; }
    public double? Current { get; set; }
    public double? Close { get; set; }
    public double? DailyGain { get; set; }
}
