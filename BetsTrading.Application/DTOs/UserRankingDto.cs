namespace BetsTrading.Application.DTOs;

public class UserRankingDto
{
    public string Id { get; set; } = string.Empty;
    public string Fullname { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public double Points { get; set; }
    public string? ProfilePic { get; set; }
    public string? Country { get; set; }
}
