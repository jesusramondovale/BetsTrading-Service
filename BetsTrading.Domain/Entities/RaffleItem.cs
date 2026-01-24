namespace BetsTrading.Domain.Entities;

public class RaffleItem
{
    private RaffleItem() { }

    public RaffleItem(int id, string name, string shortName, int coins, DateTimeOffset raffleDate, string icon, int participants)
    {
        Id = id;
        Name = name;
        ShortName = shortName;
        Coins = coins;
        RaffleDate = raffleDate;
        Icon = icon;
        Participants = participants;
    }

    public int Id { get; private set; }
    public string Name { get; private set; } = string.Empty;
    public string ShortName { get; private set; } = string.Empty;
    public int Coins { get; set; }
    public DateTimeOffset RaffleDate { get; private set; }
    public string Icon { get; private set; } = string.Empty;
    public int Participants { get; set; }
}
