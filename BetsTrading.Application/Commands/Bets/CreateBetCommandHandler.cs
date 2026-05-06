using MediatR;
using Microsoft.Extensions.DependencyInjection;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Exceptions;
using BetsTrading.Application.Interfaces;
using BCrypt.Net;

namespace BetsTrading.Application.Commands.Bets;

public class CreateBetCommandHandler : IRequestHandler<CreateBetCommand, CreateBetResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IApplicationLogger _logger;

    public CreateBetCommandHandler(
        IUnitOfWork unitOfWork,
        IServiceScopeFactory scopeFactory,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task<CreateBetResult> Handle(CreateBetCommand request, CancellationToken cancellationToken)
    {
        // Validar que el usuario existe y tiene el FCM correcto
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found");

        if (user.Fcm != request.Fcm)
            throw new InvalidOperationException("Invalid session");

        if (request.RequireStrongAuth && !request.StepUpValidated)
        {
            if (string.IsNullOrWhiteSpace(request.Password) || !BCrypt.Net.BCrypt.Verify(request.Password, user.Password))
            {
                throw new InvalidOperationException("Incorrect password");
            }
        }

        // Validar que el usuario tiene suficientes puntos
        if (user.Points < request.BetAmount)
            throw new InsufficientPointsException();

        // Obtener la zona de apuesta según la moneda
        double targetOdds;
        double targetValue;
        double targetMargin;

        if (request.Currency == "USD")
        {
            var betZoneUSD = await _unitOfWork.BetZonesUSD.GetByIdAsync(request.BetZoneId, cancellationToken);
            if (betZoneUSD == null)
                throw new InvalidOperationException("Bet zone not found");

            targetOdds = betZoneUSD.TargetOdds;
            targetValue = betZoneUSD.TargetValue;
            targetMargin = betZoneUSD.BetMargin;
        }
        else
        {
            var betZone = await _unitOfWork.BetZones.GetByIdAsync(request.BetZoneId, cancellationToken);
            if (betZone == null)
                throw new InvalidOperationException("Bet zone not found");

            targetOdds = betZone.TargetOdds;
            targetValue = betZone.TargetValue;
            targetMargin = betZone.BetMargin;
        }

        // Crear la apuesta usando el constructor de dominio
        var bet = new Bet(
            userId: request.UserId,
            ticker: request.Ticker,
            betAmount: request.BetAmount,
            originValue: request.OriginValue,
            originOdds: targetOdds,
            targetValue: targetValue,
            targetMargin: targetMargin,
            betZoneId: request.BetZoneId
        );

        // Deductir puntos del usuario
        user.DeductPoints(request.BetAmount);

        // Iniciar transacción
        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            // Guardar cambios del usuario primero
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            
            // Insertar la apuesta usando SQL directo para evitar la validación de clave foránea
            // La clave foránea bet_zone solo valida contra BetZones, pero puede referenciar BetZonesUSD también
            var betId = await _unitOfWork.Bets.InsertBetWithRawSqlAsync(bet, cancellationToken);
            
            if (betId > 0)
            {
                // Usar reflection para establecer el ID
                var idProperty = typeof(Bet).GetProperty("Id", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                idProperty?.SetValue(bet, betId);
            }

            await ReplicateCopyTradingBetsAsync(
                sourceUser: user,
                sourceBetAmount: request.BetAmount,
                ticker: request.Ticker,
                originValue: request.OriginValue,
                targetOdds: targetOdds,
                targetValue: targetValue,
                targetMargin: targetMargin,
                betZoneId: request.BetZoneId,
                cancellationToken: cancellationToken);
            
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            _logger.Debug(
                "[CreateBet] Bet created. user={UserId}, betId={BetId}, amount={Amount}, ticker={Ticker}, zoneId={ZoneId}",
                request.UserId, bet.Id, request.BetAmount, request.Ticker, request.BetZoneId);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        // Ajuste de odds en segundo plano: respuesta al cliente inmediata tras grabar la apuesta
        var betZoneId = request.BetZoneId;
        var currency = request.Currency ?? "EUR";
        _ = Task.Run(async () =>
        {
            using (var scope = _scopeFactory.CreateScope())
            {
                var updater = scope.ServiceProvider.GetRequiredService<IUpdaterService>();
                try
                {
                    await updater.UpdateOddsForBetZoneAsync(betZoneId, currency, CancellationToken.None);
                }
                catch
                {
                    // No afecta al cliente; el ajuste se puede reintentar después si hace falta
                }
            }
        });

        return new CreateBetResult
        {
            BetId = bet.Id,
            RemainingPoints = user.Points
        };
    }

    private async Task ReplicateCopyTradingBetsAsync(
        User sourceUser,
        double sourceBetAmount,
        string ticker,
        double originValue,
        double targetOdds,
        double targetValue,
        double targetMargin,
        int betZoneId,
        CancellationToken cancellationToken)
    {
        var subscriptions = await _unitOfWork.CopyTradingSubscriptions
            .GetActiveByTargetUserIdAsync(sourceUser.Id, cancellationToken);

        _logger.Debug("[CreateBet] Copy replication check. sourceUser={SourceUserId}, activeSubscriptions={Count}", sourceUser.Id, subscriptions.Count);
        if (subscriptions.Count == 0) return;

        foreach (var subscription in subscriptions)
        {
            if (!subscription.IsActive) continue;
            if (subscription.FollowerUserId == sourceUser.Id) continue;

            var follower = await _unitOfWork.Users.GetByIdAsync(subscription.FollowerUserId, cancellationToken);
            if (follower == null || !follower.IsActive)
            {
                subscription.Stop("follower_not_available");
                _unitOfWork.CopyTradingSubscriptions.Update(subscription);
                _logger.Debug("[CreateBet] Subscription stopped: follower not available. follower={FollowerId}, target={TargetId}", subscription.FollowerUserId, sourceUser.Id);
                continue;
            }

            var percent = subscription.AutoAdjustByBalance
                ? CalculateAutoPercent(follower.Points, sourceUser.Points)
                : subscription.CopyPercent;

            if (percent <= 0)
            {
                _logger.Debug("[CreateBet] Replication skipped: non-positive percent. follower={FollowerId}, target={TargetId}", follower.Id, sourceUser.Id);
                continue;
            }

            var followerAmount = Math.Round(sourceBetAmount * (percent / 100.0), 2, MidpointRounding.AwayFromZero);
            if (followerAmount <= 0)
            {
                _logger.Debug("[CreateBet] Replication skipped: computed amount <= 0. follower={FollowerId}, sourceAmount={SourceAmount}, percent={Percent}", follower.Id, sourceBetAmount, percent);
                continue;
            }
            if (follower.Points < followerAmount)
            {
                _logger.Debug("[CreateBet] Replication skipped: insufficient follower points. follower={FollowerId}, followerPoints={FollowerPoints}, required={Required}", follower.Id, follower.Points, followerAmount);
                continue;
            }

            follower.DeductPoints(followerAmount);
            var copyBet = new Bet(
                userId: follower.Id,
                ticker: ticker,
                betAmount: followerAmount,
                originValue: originValue,
                originOdds: targetOdds,
                targetValue: targetValue,
                targetMargin: targetMargin,
                betZoneId: betZoneId);

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.Bets.InsertBetWithRawSqlAsync(copyBet, cancellationToken);
            subscription.MarkCopiedNow();
            _unitOfWork.CopyTradingSubscriptions.Update(subscription);
            _logger.Debug("[CreateBet] Replicated copy bet. follower={FollowerId}, target={TargetId}, amount={Amount}, zoneId={ZoneId}", follower.Id, sourceUser.Id, followerAmount, betZoneId);
        }
    }

    private static double CalculateAutoPercent(double followerPoints, double sourcePoints)
    {
        if (sourcePoints <= 0 || followerPoints <= 0) return 0;
        return (followerPoints / sourcePoints) * 100.0;
    }
}
