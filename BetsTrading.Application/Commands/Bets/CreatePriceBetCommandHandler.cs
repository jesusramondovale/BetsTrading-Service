using MediatR;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Exceptions;
using BetsTrading.Application.Services;

namespace BetsTrading.Application.Commands.Bets;

public class CreatePriceBetCommandHandler : IRequestHandler<CreatePriceBetCommand, CreatePriceBetResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreatePriceBetCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CreatePriceBetResult> Handle(CreatePriceBetCommand request, CancellationToken cancellationToken)
    {
        // Validar que el usuario existe y tiene el FCM correcto
        var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (user == null)
            throw new InvalidOperationException("Unexistent user or session expired!");

        if (user.Fcm != request.Fcm)
            throw new InvalidOperationException("Invalid session");

        // Calcular costo de la apuesta
        int betCost = PriceBetCostService.GetBetCostFromMargin(request.Margin);

        // Validar que el usuario tiene suficientes puntos
        if (user.Points < betCost)
            throw new BetException("NO POINTS");

        // Validar fecha mínima
        if (request.EndDate < DateTime.UtcNow.AddDays(PriceBetCostService.GetDaysMargin()))
            throw new BetException("NO TIME");

        // Verificar si ya existe una apuesta para este ticker y fecha
        if (request.Currency == "EUR")
        {
            var existingBet = await _unitOfWork.PriceBets.GetByTickerAndEndDateAsync(request.Ticker, request.EndDate, cancellationToken);
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

            // Deductir puntos del usuario
            user.DeductPoints(betCost);

            // Iniciar transacción
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await _unitOfWork.PriceBets.AddAsync(newPriceBet, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            return new CreatePriceBetResult
            {
                PriceBetId = newPriceBet.Id,
                RemainingPoints = user.Points
            };
        }
        else
        {
            var existingBet = await _unitOfWork.PriceBetsUSD.GetByTickerAndEndDateAsync(request.Ticker, request.EndDate, cancellationToken);
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

            // Deductir puntos del usuario
            user.DeductPoints(betCost);

            // Iniciar transacción
            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                await _unitOfWork.PriceBetsUSD.AddAsync(newPriceBet, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }

            return new CreatePriceBetResult
            {
                PriceBetId = newPriceBet.Id,
                RemainingPoints = user.Points
            };
        }
    }
}
