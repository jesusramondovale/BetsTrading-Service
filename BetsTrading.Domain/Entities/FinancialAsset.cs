namespace BetsTrading.Domain.Entities;

public class FinancialAsset
{
    private FinancialAsset() { }

    public FinancialAsset(string name, string group, string ticker, double currentEur, double currentUsd)
    {
        Name = name;
        Group = group;
        Ticker = ticker;
        CurrentEur = currentEur;
        CurrentUsd = currentUsd;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string Group { get; private set; } = string.Empty;
    public string? Icon { get; private set; }
    public string? Country { get; private set; }
    public string Ticker { get; private set; } = string.Empty;
    public double CurrentEur { get; private set; }
    public double CurrentUsd { get; private set; }
    public double? CurrentMaxOdd { get; private set; }
    public int? CurrentMaxOddDirection { get; private set; }

    public void UpdateCurrentMaxOdd(double maxOdd, int direction)
    {
        CurrentMaxOdd = maxOdd;
        CurrentMaxOddDirection = direction;
    }

    public void ClearCurrentMaxOdd()
    {
        CurrentMaxOdd = null;
        CurrentMaxOddDirection = null;
    }

    public void UpdateCurrentPrice(double eur, double usd)
    {
        CurrentEur = eur;
        CurrentUsd = usd;
    }
}
