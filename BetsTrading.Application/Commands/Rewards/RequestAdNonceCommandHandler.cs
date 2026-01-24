using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Commands.Rewards;

public class RequestAdNonceCommandHandler : IRequestHandler<RequestAdNonceCommand, RequestAdNonceResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public RequestAdNonceCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<RequestAdNonceResult> Handle(RequestAdNonceCommand request, CancellationToken cancellationToken)
    {
        try
        {
            // Clean up expired nonces
            var expired = await _unitOfWork.RewardNonces.GetExpiredUnusedAsync(cancellationToken);
            if (expired.Any())
            {
                foreach (var expiredNonce in expired)
                {
                    _unitOfWork.RewardNonces.Remove(expiredNonce);
                }
                await _unitOfWork.SaveChangesAsync(cancellationToken);
            }

            // Check outstanding nonces limit (max 3)
            var outstanding = await _unitOfWork.RewardNonces.CountOutstandingByUserIdAsync(request.UserId, cancellationToken);
            if (outstanding >= 3)
            {
                _logger.Warning("[REWARDS] :: RequestAdNonce :: Too many pending nonces for user {0}", request.UserId);
                return new RequestAdNonceResult
                {
                    Success = false,
                    Message = "too_many_pending_nonces"
                };
            }

            // Create new nonce
            var nonce = new RewardNonce(
                request.UserId,
                request.AdUnitId,
                request.Purpose,
                request.Coins
            );

            await _unitOfWork.RewardNonces.AddAsync(nonce, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);

            _logger.Information("[REWARDS] :: RequestAdNonce :: Created nonce for user {0}, adUnit {1}", request.UserId, request.AdUnitId);

            return new RequestAdNonceResult
            {
                Success = true,
                Nonce = nonce.Nonce,
                ExpiresAt = nonce.ExpiresAt
            };
        }
        catch (Exception ex)
        {
            var inner = ex.InnerException != null ? $", Inner: {ex.InnerException.Message}" : "";
            _logger.Error(ex, "[REWARDS] :: RequestAdNonce :: Error: {0} ({1}){2}", ex.Message, ex.GetType().Name, inner);
            return new RequestAdNonceResult
            {
                Success = false,
                Message = "Internal server error"
            };
        }
    }
}
