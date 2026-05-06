using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;
using MediatR;

namespace BetsTrading.Application.Commands.Bets;

public class ConfigureCopyTradingCommandHandler : IRequestHandler<ConfigureCopyTradingCommand, ConfigureCopyTradingResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public ConfigureCopyTradingCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<ConfigureCopyTradingResult> Handle(ConfigureCopyTradingCommand request, CancellationToken cancellationToken)
    {
        var followerId = request.FollowerUserId.Trim();
        var targetId = request.TargetUserId.Trim();
        _logger.Debug(
            "[ConfigureCopyTrading] Start. follower={FollowerId}, target={TargetId}, enabled={Enabled}, percent={Percent}, autoAdjust={AutoAdjust}, stopAfterOneLoss={StopAfterOneLoss}",
            followerId, targetId, request.IsEnabled, request.CopyPercent, request.AutoAdjustByBalance, request.StopAfterOneLoss);

        if (string.IsNullOrWhiteSpace(followerId) || string.IsNullOrWhiteSpace(targetId))
        {
            throw new InvalidOperationException("FollowerUserId and TargetUserId are required");
        }

        if (followerId == targetId)
        {
            throw new InvalidOperationException("User cannot copy himself");
        }

        var follower = await _unitOfWork.Users.GetByIdAsync(followerId, cancellationToken);
        if (follower == null || follower.Fcm != request.Fcm)
        {
            _logger.Debug("[ConfigureCopyTrading] Invalid session for follower={FollowerId}", followerId);
            throw new InvalidOperationException("Invalid session");
        }

        var target = await _unitOfWork.Users.GetByIdAsync(targetId, cancellationToken);
        if (target == null)
        {
            _logger.Debug("[ConfigureCopyTrading] Target user not found. target={TargetId}", targetId);
            throw new InvalidOperationException("Target user not found");
        }

        var existing = await _unitOfWork.CopyTradingSubscriptions
            .GetByFollowerAndTargetAsync(followerId, targetId, cancellationToken);

        if (!request.IsEnabled)
        {
            if (existing != null && existing.IsActive)
            {
                existing.Stop("manual_disable");
                _unitOfWork.CopyTradingSubscriptions.Update(existing);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                _logger.Debug("[ConfigureCopyTrading] Existing subscription disabled. follower={FollowerId}, target={TargetId}", followerId, targetId);
            }
            else
            {
                _logger.Debug("[ConfigureCopyTrading] Disable requested but no active subscription found. follower={FollowerId}, target={TargetId}", followerId, targetId);
            }

            return new ConfigureCopyTradingResult
            {
                Active = false,
                Message = "Copy-trading disabled"
            };
        }

        var copyPercent = Math.Clamp(request.CopyPercent, 1, 100);
        if (existing == null)
        {
            existing = new CopyTradingSubscription(
                followerUserId: followerId,
                targetUserId: targetId,
                copyPercent: copyPercent,
                autoAdjustByBalance: request.AutoAdjustByBalance,
                stopAfterOneLoss: request.StopAfterOneLoss);
            await _unitOfWork.CopyTradingSubscriptions.AddAsync(existing, cancellationToken);
            _logger.Debug("[ConfigureCopyTrading] Created subscription. follower={FollowerId}, target={TargetId}, percent={Percent}", followerId, targetId, copyPercent);
        }
        else
        {
            existing.UpdateSettings(copyPercent, request.AutoAdjustByBalance, request.StopAfterOneLoss);
            _unitOfWork.CopyTradingSubscriptions.Update(existing);
            _logger.Debug("[ConfigureCopyTrading] Updated subscription. follower={FollowerId}, target={TargetId}, percent={Percent}", followerId, targetId, copyPercent);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
        _logger.Debug("[ConfigureCopyTrading] Saved successfully. follower={FollowerId}, target={TargetId}", followerId, targetId);

        return new ConfigureCopyTradingResult
        {
            Active = true,
            Message = "Copy-trading enabled"
        };
    }
}
