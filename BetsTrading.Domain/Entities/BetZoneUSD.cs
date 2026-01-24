namespace BetsTrading.Domain.Entities;

public class BetZoneUSD
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

    public BetZoneUSD(string ticker, double targetValue, double betMargin, DateTime startDate, DateTime endDate, double targetOdds, int betType, int timeframe)
    {
        Ticker = ticker;
        TargetValue = targetValue;
        BetMargin = betMargin;
        StartDate = startDate;
        EndDate = endDate;
        TargetOdds = targetOdds;
        BetType = betType;
        Active = true;
        Timeframe = timeframe;
    }

    public void UpdateTargetOdds(double newOdds)
    {
        TargetOdds = newOdds;
    }
}
