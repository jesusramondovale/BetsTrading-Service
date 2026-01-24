using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetTrendsQuery : IRequest<GetTrendsResult>
{
    public string? UserId { get; set; }
    public string Currency { get; set; } = "EUR";
    public string GetUserId() => UserId ?? string.Empty;
}

public class GetTrendsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<TrendDto> Trends { get; set; } = new();
}
