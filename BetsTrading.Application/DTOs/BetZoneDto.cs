namespace BetsTrading.Application.DTOs;

public class BetZoneDto
{
    public int Id { get; set; }
    public string Ticker { get; set; } = string.Empty;
    public double TargetValue { get; set; }
    public double BetMargin { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public double TargetOdds { get; set; }
    public int BetType { get; set; }
    public bool Active { get; set; }
    public int Timeframe { get; set; }
}
