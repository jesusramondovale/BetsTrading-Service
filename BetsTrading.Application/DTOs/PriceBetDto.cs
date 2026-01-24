namespace BetsTrading.Application.DTOs;

public class PriceBetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public double PriceBet { get; set; }
    public bool Paid { get; set; }
    public int Prize { get; set; }
    public double Margin { get; set; }
    public string UserId { get; set; } = string.Empty;
    public DateTime BetDate { get; set; }
    public DateTime EndDate { get; set; }
    public string IconPath { get; set; } = string.Empty;
    public bool Archived { get; set; }
}
