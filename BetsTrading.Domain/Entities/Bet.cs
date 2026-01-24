namespace BetsTrading.Domain.Entities;

public class Bet
{
    // Constructor privado para EF Core
    private Bet() { }

    // Constructor para crear nuevas apuestas
    public Bet(string userId, string ticker, double betAmount, double originValue, 
               double originOdds, double targetValue, double targetMargin, int betZoneId)
    {
        UserId = userId;
        Ticker = ticker;
        BetAmount = betAmount;
        OriginValue = originValue;
        OriginOdds = originOdds;
        TargetValue = targetValue;
        TargetMargin = targetMargin;
        BetZoneId = betZoneId;
        TargetWon = false;
        Finished = false;
        Paid = false;
        Archived = false;
    }

    public int Id { get; private set; }
    public string UserId { get; private set; } = string.Empty;
    public string Ticker { get; private set; } = string.Empty;
    public double BetAmount { get; private set; }
    public double OriginValue { get; private set; }
    public double OriginOdds { get; private set; }
    public double TargetValue { get; private set; }
    public double TargetMargin { get; private set; }
    public bool TargetWon { get; private set; }
    public bool Finished { get; private set; }
    public bool Paid { get; private set; }
    public int BetZoneId { get; private set; }
    public bool Archived { get; private set; }

    // Métodos de dominio
    public void MarkAsWon()
    {
        if (Finished)
            throw new InvalidOperationException("Cannot mark a finished bet as won");
        
        TargetWon = true;
        Finished = true;
    }

    public void MarkAsLost()
    {
        if (Finished)
            throw new InvalidOperationException("Cannot mark a finished bet as lost");
        
        TargetWon = false;
        Finished = true;
    }

    public void MarkAsPaid()
    {
        if (!Finished)
            throw new InvalidOperationException("Cannot pay an unfinished bet");
        
        if (Paid)
            throw new InvalidOperationException("Bet is already paid");
        
        Paid = true;
    }

    public void Archive()
    {
        Archived = true;
    }
}
