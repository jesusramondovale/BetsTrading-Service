using MediatR;

namespace BetsTrading.Application.Commands.Raffles;

public class CreateRaffleCommand : IRequest<CreateRaffleResult>
{
    public string? UserId { get; set; }
    public string? ItemToken { get; set; }

    public string GetUserId() => UserId ?? string.Empty;
    public string GetItemToken() => ItemToken ?? string.Empty;
}

public class CreateRaffleResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long? RaffleId { get; set; }
}
