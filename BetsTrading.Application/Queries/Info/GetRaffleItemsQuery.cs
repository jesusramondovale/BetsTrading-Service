using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetRaffleItemsQuery : IRequest<GetRaffleItemsResult>
{
    public string? UserId { get; set; }
    public string GetUserId() => UserId ?? string.Empty;
}

public class GetRaffleItemsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<RaffleItemDto> Items { get; set; } = new();
}
