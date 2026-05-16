namespace BetsTrading.Application.DTOs;

public class AdminRaffleItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string ShortName { get; set; } = string.Empty;
    public int Coins { get; set; }
    public DateTimeOffset RaffleDate { get; set; }
    public string Icon { get; set; } = string.Empty;
}

public class SaveAdminRaffleItemsRequest
{
    public List<AdminRaffleItemDto> Items { get; set; } = new();
}
