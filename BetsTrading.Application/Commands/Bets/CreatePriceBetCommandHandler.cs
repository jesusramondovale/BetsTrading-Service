using MediatR;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Exceptions;
using BetsTrading.Application.Interfaces;
using BetsTrading.Application.Services;
using BCrypt.Net;

namespace BetsTrading.Application.Commands.Bets;

public class CreatePriceBetCommandHandler : IRequestHandler<CreatePriceBetCommand, CreatePriceBetResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public CreatePriceBetCommandHandler(
        IUnitOfWork unitOfWork,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<CreatePriceBetResult> Handle(CreatePriceBetCommand request, CancellationToken cancellationToken)
    {
        // Validar que el usuario existe y tiene el FCM correcto
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("Unexistent user or session expired!");

        // Calcular costo de la apuesta
        int betCost = PriceBetCostService.GetBetCostFromMargin(request.Margin);

        if (request.RequireStrongAuth && !request.StepUpValidated)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new InvalidOperationException("Incorrect password");
            }
        }

        // Validar que el usuario tiene suficientes puntos
        if (user.Points < betCost)
            throw new BetException("NO POINTS");

        // Validar fecha mínima
        if (request.EndDate < DateTime.UtcNow.AddDays(PriceBetCostService.GetDaysMargin()))
            throw new BetException("NO TIME");

        // Verificar si ya existe una apuesta para este ticker y fecha
        if (request.Currency == "EUR")
        {
            var existingBet = await _unitOfWork.PriceBets.GetByTickerAndEndDateAsync(request.UserId, request.Ticker, request.EndDate, cancellationToken);
            if (existingBet != null)
                throw new BetException("EXISTING BET");

            // Crear nueva apuesta de precio EUR
            var newPriceBet = new PriceBet(
                userId: request.UserId,
                ticker: request.Ticker,
                priceBet: request.PriceBet,
                prize: PriceBetCostService.GetPrize(),
                margin: request.Margin,
                endDate: request.EndDate
            );

            // Iniciar transacción
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (!await _unitOfWork.Users.TryDeductPointsAsync(request.UserId, betCost, cancellationToken))
                    throw new BetException("NO POINTS");

                _unitOfWork.Users.DetachTracked(request.UserId);
                await _unitOfWork.PriceBets.AddAsync(newPriceBet, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await ReplicatePriceBetsAsync(request, user, betCost, cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                _logger.Debug("[CreatePriceBet] Price bet created (EUR). user={UserId}, priceBetId={PriceBetId}, ticker={Ticker}, margin={Margin}, cost={Cost}",
                    request.UserId, newPriceBet.Id, request.Ticker, request.Margin, betCost);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            var updatedUserEur = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            return new CreatePriceBetResult
            {
                PriceBetId = newPriceBet.Id,
                RemainingPoints = updatedUserEur?.Points ?? 0
            };
        }
        else
        {
            var existingBet = await _unitOfWork.PriceBetsUSD.GetByTickerAndEndDateAsync(request.UserId, request.Ticker, request.EndDate, cancellationToken);
            if (existingBet != null)
                throw new BetException("EXISTING BET");

            // Crear nueva apuesta de precio USD
            var newPriceBet = new PriceBetUSD(
                userId: request.UserId,
                ticker: request.Ticker,
                priceBet: request.PriceBet,
                prize: PriceBetCostService.GetPrize(),
                margin: request.Margin,
                endDate: request.EndDate
            );

            // Iniciar transacción
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                if (!await _unitOfWork.Users.TryDeductPointsAsync(request.UserId, betCost, cancellationToken))
                    throw new BetException("NO POINTS");

                _unitOfWork.Users.DetachTracked(request.UserId);
                await _unitOfWork.PriceBetsUSD.AddAsync(newPriceBet, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await ReplicatePriceBetsAsync(request, user, betCost, cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                _logger.Debug("[CreatePriceBet] Price bet created (USD). user={UserId}, priceBetId={PriceBetId}, ticker={Ticker}, margin={Margin}, cost={Cost}",
                    request.UserId, newPriceBet.Id, request.Ticker, request.Margin, betCost);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            var updatedUserUsd = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            return new CreatePriceBetResult
            {
                PriceBetId = newPriceBet.Id,
                RemainingPoints = updatedUserUsd?.Points ?? 0
            };
        }
    }

    private async Task ReplicatePriceBetsAsync(
        CreatePriceBetCommand request,
        User sourceUser,
        int sourceBetCost,
        CancellationToken cancellationToken)
    {
        var subscriptions = await _unitOfWork.CopyTradingSubscriptions
            .GetActiveByTargetUserIdAsync(sourceUser.Id, cancellationToken);

        _logger.Debug("[CreatePriceBet] Copy replication check. sourceUser={SourceUserId}, activeSubscriptions={Count}", sourceUser.Id, subscriptions.Count);
        if (subscriptions.Count == 0) return;

        var freshSource = await _unitOfWork.Users.GetByIdAsync(sourceUser.Id, cancellationToken);
        var sourcePoints = freshSource?.Points ?? 0;

        foreach (var subscription in subscriptions)
        {
            if (!subscription.IsActive) continue;
            if (subscription.FollowerUserId == sourceUser.Id) continue;

            var follower = await _unitOfWork.Users.GetByIdAsync(subscription.FollowerUserId, cancellationToken);
            if (follower == null)
            {
                subscription.Stop("follower_not_found");
                _unitOfWork.CopyTradingSubscriptions.Update(subscription);
                _logger.Debug("[CreatePriceBet] Subscription stopped: follower user not found. follower={FollowerId}, target={TargetId}", subscription.FollowerUserId, sourceUser.Id);
                continue;
            }

            var percent = subscription.AutoAdjustByBalance
                ? CalculateAutoPercent(follower.Points, sourcePoints)
                : subscription.CopyPercent;
            if (percent <= 0)
            {
                _logger.Debug("[CreatePriceBet] Replication skipped: non-positive percent. follower={FollowerId}, target={TargetId}", follower.Id, sourceUser.Id);
                continue;
            }

            var followerBudget = sourceBetCost * (percent / 100.0);
            var margin = ResolveMarginByBudget(followerBudget);
            if (margin < 0)
            {
                _logger.Debug("[CreatePriceBet] Replication skipped: insufficient budget for any margin. follower={FollowerId}, budget={Budget}", follower.Id, followerBudget);
                continue;
            }
            var followerCost = PriceBetCostService.GetBetCostFromMargin(margin);
            if (followerCost <= 0)
            {
                _logger.Debug("[CreatePriceBet] Replication skipped: followerCost <= 0. follower={FollowerId}, margin={Margin}", follower.Id, margin);
                continue;
            }
            if (!await _unitOfWork.Users.TryDeductPointsAsync(follower.Id, followerCost, cancellationToken))
            {
                _logger.Debug("[CreatePriceBet] Replication skipped: insufficient follower points. follower={FollowerId}, required={Required}", follower.Id, followerCost);
                continue;
            }

            _unitOfWork.Users.DetachTracked(follower.Id);

            if (request.Currency == "EUR")
            {
                var existingFollowerBet = await _unitOfWork.PriceBets.GetByTickerAndEndDateAsync(
                    follower.Id,
                    request.Ticker,
                    request.EndDate,
                    cancellationToken);
                if (existingFollowerBet != null)
                {
                    _logger.Debug("[CreatePriceBet] Replication skipped: follower already has same EUR price bet. follower={FollowerId}, ticker={Ticker}, endDate={EndDate}", follower.Id, request.Ticker, request.EndDate);
                    continue;
                }

                var followerPriceBet = new PriceBet(
                    userId: follower.Id,
                    ticker: request.Ticker,
                    priceBet: request.PriceBet,
                    prize: PriceBetCostService.GetPrize(),
                    margin: margin,
                    endDate: request.EndDate);
                await _unitOfWork.PriceBets.AddAsync(followerPriceBet, cancellationToken);
            }
            else
            {
                var existingFollowerBet = await _unitOfWork.PriceBetsUSD.GetByTickerAndEndDateAsync(
                    follower.Id,
                    request.Ticker,
                    request.EndDate,
                    cancellationToken);
                if (existingFollowerBet != null)
                {
                    _logger.Debug("[CreatePriceBet] Replication skipped: follower already has same USD price bet. follower={FollowerId}, ticker={Ticker}, endDate={EndDate}", follower.Id, request.Ticker, request.EndDate);
                    continue;
                }

                var followerPriceBet = new PriceBetUSD(
                    userId: follower.Id,
                    ticker: request.Ticker,
                    priceBet: request.PriceBet,
                    prize: PriceBetCostService.GetPrize(),
                    margin: margin,
                    endDate: request.EndDate);
                await _unitOfWork.PriceBetsUSD.AddAsync(followerPriceBet, cancellationToken);
            }

            subscription.MarkCopiedNow();
            _unitOfWork.CopyTradingSubscriptions.Update(subscription);
            _logger.Debug("[CreatePriceBet] Replicated copy price bet. follower={FollowerId}, target={TargetId}, margin={Margin}, cost={Cost}", follower.Id, sourceUser.Id, margin, followerCost);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private static double CalculateAutoPercent(double followerPoints, double sourcePoints)
    {
        if (sourcePoints <= 0 || followerPoints <= 0) return 0;
        return (followerPoints / sourcePoints) * 100.0;
    }

    private static double ResolveMarginByBudget(double followerBudget)
    {
        var margins = new[] { 0.1, 0.075, 0.05, 0.01, 0.0 };
        foreach (var margin in margins)
        {
            if (PriceBetCostService.GetBetCostFromMargin(margin) <= followerBudget)
            {
                return margin;
            }
        }

        return -1;
    }
}
