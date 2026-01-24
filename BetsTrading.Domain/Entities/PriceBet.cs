namespace BetsTrading.Domain.Entities;

public class PriceBet
{
    // Constructor privado para EF Core
    private PriceBet() { }

    // Constructor para crear nuevas apuestas de precio
    public PriceBet(string userId, string ticker, double priceBet, int prize, double margin, DateTime endDate)
    {
        UserId = userId;
        Ticker = ticker;
        PriceBetValue = priceBet;
        Prize = prize;
        Margin = margin;
        EndDate = endDate;
        BetDate = DateTime.UtcNow;
        Paid = false;
        Archived = false;
    }

    public int Id { get; set; }
    public string UserId { get; private set; } = string.Empty;
    public string Ticker { get; private set; } = string.Empty;
    public double PriceBetValue { get; private set; }
    public double Margin { get; private set; }
    public bool Paid { get; private set; }
    public DateTime BetDate { get; private set; }
    public DateTime EndDate { get; private set; }
    public bool Archived { get; private set; }
    public int Prize { get; private set; }

    // Métodos de dominio
    public void MarkAsPaid()
    {
        if (Paid)
            throw new InvalidOperationException("Price bet is already paid");
        
        Paid = true;
    }

    public void Archive()
    {
        Archived = true;
    }
}
