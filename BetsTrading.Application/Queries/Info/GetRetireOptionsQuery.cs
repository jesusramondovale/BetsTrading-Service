using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetRetireOptionsQuery : IRequest<GetRetireOptionsResult>
{
    public string UserId { get; set; } = string.Empty;
}

public class GetRetireOptionsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<WithdrawalMethodDto> Options { get; set; } = new();
}
