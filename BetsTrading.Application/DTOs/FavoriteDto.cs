namespace BetsTrading.Application.DTOs;

public class FavoriteDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public double DailyGain { get; set; }
    public double Close { get; set; }
    public double Current { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
}
