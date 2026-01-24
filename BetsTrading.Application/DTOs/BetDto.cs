namespace BetsTrading.Application.DTOs;

public class BetDto
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public double BetAmount { get; set; }
    public double NecessaryGain { get; set; }
    public double OriginValue { get; set; }
    public double CurrentValue { get; set; }
    public double TargetValue { get; set; }
    public double TargetMargin { get; set; }
    public DateTime? TargetDate { get; set; }
    public DateTime? EndDate { get; set; }
    public double TargetOdds { get; set; }
    public bool TargetWon { get; set; }
    public bool Finished { get; set; }
    public string IconPath { get; set; } = string.Empty;
    public int Type { get; set; }
    public int DateMargin { get; set; }
    public int BetZone { get; set; }
    public bool Archived { get; set; }
}
