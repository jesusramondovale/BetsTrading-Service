using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Bets;

public sealed class GetPublicCopyBettingSnapshotQuery : IRequest<PublicCopyBettingSnapshotDto>
{
    public string TargetUserId { get; set; } = string.Empty;
}
