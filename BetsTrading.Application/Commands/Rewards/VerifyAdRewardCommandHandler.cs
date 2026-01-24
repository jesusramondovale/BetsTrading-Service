using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using BetsTrading.Application.Interfaces;
using BetsTrading.Application.Services;

namespace BetsTrading.Application.Commands.Rewards;

public class VerifyAdRewardCommandHandler : IRequestHandler<VerifyAdRewardCommand, VerifyAdRewardResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public VerifyAdRewardCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<VerifyAdRewardResult> Handle(VerifyAdRewardCommand request, CancellationToken cancellationToken)
    {
        // Verify SSV signature if provided
        if (!string.IsNullOrEmpty(request.Signature) && !string.IsNullOrEmpty(request.KeyId))
        {
            var isValid = await AdMobSsvVerifier.VerifySignatureAsync(
                request.RawQuery ?? string.Empty,
                request.Signature,
                request.KeyId,
                cancellationToken);

            if (!isValid)
            {
                _logger.Warning("[REWARDS] :: VerifyAdReward :: Invalid SSV signature");
                return new VerifyAdRewardResult
                {
                    Success = false,
                    Message = "Invalid signature"
                };
            }
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Check if transaction already exists (idempotency)
            var existingTransaction = await _unitOfWork.RewardTransactions.ExistsByTransactionIdAsync(request.TransactionId, cancellationToken);
            if (existingTransaction)
            {
                _logger.Information("[REWARDS] :: VerifyAdReward :: Transaction already processed: {0}", request.TransactionId);
                return new VerifyAdRewardResult
                {
                    Success = true,
                    Message = "Transaction already processed"
                };
            }

            // Validate nonce
            var nonce = await _unitOfWork.RewardNonces.GetByNonceAsync(request.Nonce, cancellationToken);
            if (nonce == null || nonce.Used || nonce.IsExpired() || nonce.UserId != request.UserId)
            {
                _logger.Warning("[REWARDS] :: VerifyAdReward :: Invalid nonce for user {0}", request.UserId);
                return new VerifyAdRewardResult
                {
                    Success = false,
                    Message = "invalid_nonce"
                };
            }

            // Get user
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                _logger.Warning("[REWARDS] :: VerifyAdReward :: User not found: {0}", request.UserId);
                return new VerifyAdRewardResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            // Calculate coins to award (use nonce value or rewardAmountRaw as fallback)
            var coinsToAward = nonce.Coins ?? (int)(request.RewardAmountRaw ?? 0);

            // Award coins to user
            user.AddPoints(coinsToAward);

            // Mark nonce as used
            nonce.MarkAsUsed();

            // Create reward transaction
            var transaction = new RewardTransaction(
                request.TransactionId,
                request.UserId,
                coinsToAward,
                request.AdUnitId ?? nonce.AdUnitId,
                request.RewardItem,
                request.RewardAmountRaw,
                request.SsvKeyId,
                request.RawQuery
            );

            await _unitOfWork.RewardTransactions.AddAsync(transaction, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.Information("[REWARDS] :: VerifyAdReward :: Awarded {0} coins to user {1} via transaction {2}", 
                coinsToAward, request.UserId, request.TransactionId);

            return new VerifyAdRewardResult
            {
                Success = true,
                Message = "Reward verified and processed"
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.Error(ex, "[REWARDS] :: VerifyAdReward :: Error: {0}", ex.Message);
            return new VerifyAdRewardResult
            {
                Success = false,
                Message = "Internal server error"
            };
        }
    }
}
