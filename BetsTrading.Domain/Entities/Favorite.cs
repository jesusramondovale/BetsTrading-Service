namespace BetsTrading.Domain.Entities;

public class Favorite
{
    private Favorite() { }

    public Favorite(string id, string userId, string ticker)
    {
        Id = id;
        UserId = userId;
        Ticker = ticker;
    }

    public string Id { get; private set; } = string.Empty;
    public string UserId { get; private set; } = string.Empty;
    public string Ticker { get; private set; } = string.Empty;
}
