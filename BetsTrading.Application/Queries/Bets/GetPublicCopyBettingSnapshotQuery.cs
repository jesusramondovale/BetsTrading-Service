using MediatR;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Bets;

public sealed class GetPublicCopyBettingSnapshotQuery : IRequest<PublicCopyBettingSnapshotDto>
{
    public string TargetUserId { get; set; } = string.Empty;

    /// <summary>Usuario autenticado que consulta el perfil (follower potencial).</summary>
    public string? ViewerUserId { get; set; }
}
