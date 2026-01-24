namespace BetsTrading.Domain.Entities;

public class BetZone
{
    // Constructor privado para EF Core
    private BetZone() { }

    public BetZone(string ticker, double targetValue, double betMargin, 
                   DateTime startDate, DateTime endDate, double targetOdds, 
                   int betType, int timeframe)
    {
        Ticker = ticker;
        TargetValue = targetValue;
        BetMargin = betMargin;
        StartDate = startDate;
        EndDate = endDate;
        TargetOdds = targetOdds;
        BetType = betType;
        Timeframe = timeframe;
        Active = true;
    }

    public int Id { get; private set; }
    public string Ticker { get; private set; } = string.Empty;
    public double TargetValue { get; private set; }
    public double BetMargin { get; private set; }
    public DateTime StartDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public double TargetOdds { get; private set; }
    public int BetType { get; private set; }
    public bool Active { get; private set; }
    public int Timeframe { get; private set; }

    public void Deactivate()
    {
        Active = false;
    }

    public bool IsExpired()
    {
        return DateTime.UtcNow > EndDate;
    }

    public void UpdateTargetOdds(double newOdds)
    {
        TargetOdds = newOdds;
    }
}
