using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetUserInfoQuery : IRequest<UserInfoDto?>
{
    public string UserId { get; set; } = string.Empty;
}

public class UserInfoDto
{
    public string Username { get; set; } = string.Empty;
    public bool IsVerified { get; set; }
    public string Email { get; set; } = string.Empty;
    public DateTime Birthday { get; set; }
    public string Fullname { get; set; } = string.Empty;
    public string Country { get; set; } = string.Empty;
    public DateTime LastSession { get; set; }
    public string? ProfilePic { get; set; }
    public double Points { get; set; }
}
