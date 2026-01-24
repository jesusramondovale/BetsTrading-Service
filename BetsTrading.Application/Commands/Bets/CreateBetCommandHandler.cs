using MediatR;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Exceptions;

namespace BetsTrading.Application.Commands.Bets;

public class CreateBetCommandHandler : IRequestHandler<CreateBetCommand, CreateBetResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateBetCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateBetResult> Handle(CreateBetCommand request, CancellationToken cancellationToken)
    {
        // Validar que el usuario existe y tiene el FCM correcto
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("User not found");

        if (user.Fcm != request.Fcm)
            throw new InvalidOperationException("Invalid session");

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
            
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }

        return new CreateBetResult
        {
            BetId = bet.Id,
            RemainingPoints = user.Points
        };
    }
}
