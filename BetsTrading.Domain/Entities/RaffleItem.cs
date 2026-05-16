using BetsTrading.Domain;

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

    public void UpdateFromAdmin(string name, string shortName, int coins, DateTimeOffset raffleDate, string? newIconBase64)
    {
        Name = name.Trim();
        ShortName = shortName.Trim();
        Coins = coins;
        RaffleDate = raffleDate;
        if (!string.IsNullOrWhiteSpace(newIconBase64))
            Icon = newIconBase64;
    }

    public void ResetAfterDraw(DateTime utcNow)
    {
        Participants = 0;
        RaffleDate = RaffleSchedule.NextMonthFirstUtc(utcNow);
    }

    public static RaffleItem CreateDefault(int id, DateTime utcNow)
    {
        var next = RaffleSchedule.NextMonthFirstUtc(utcNow);
        return new RaffleItem(
            id,
            $"Prize {id}",
            $"P{id}",
            100 * id,
            next,
            string.Empty,
            0);
    }
}
