namespace BetsTrading.Domain.Entities;

public class Raffle
{
    private Raffle() { }

    public Raffle(string itemId, string userId, DateTime raffleDate)
    {
        ItemId = itemId;
        UserId = userId;
        RaffleDate = raffleDate;
    }

    public long Id { get; private set; }
    public string ItemId { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public DateTime RaffleDate { get; private set; }
}
