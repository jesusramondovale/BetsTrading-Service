namespace BetsTrading.Domain.Entities;

public class Trend
{
    public int Id { get; set; }
    public double DailyGain { get; private set; }
    public string Ticker { get; private set; } = string.Empty;

    // Constructor para EF Core
    private Trend() { }

    public Trend(int id, double dailyGain, string ticker)
    {
        Id = id;
        DailyGain = dailyGain;
        Ticker = ticker;
    }
}
