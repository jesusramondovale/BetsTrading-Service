using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Bets;

public class DeleteHistoricBetsCommandHandler : IRequestHandler<DeleteHistoricBetsCommand, int>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteHistoricBetsCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<int> Handle(DeleteHistoricBetsCommand request, CancellationToken cancellationToken)
    {
        // Obtener todas las apuestas del usuario que ya terminaron (zonas con end_date < ahora)
        var now = DateTime.UtcNow;
        var allBets = await _unitOfWork.Bets.GetUserBetsAsync(request.UserId, includeArchived: true, cancellationToken);
        
        var historicBets = new List<Domain.Entities.Bet>();
        foreach (var bet in allBets)
        {
            // Verificar si la zona de apuesta ya terminó
            var betZone = await _unitOfWork.BetZones.GetByIdAsync(bet.BetZoneId, cancellationToken);
            if (betZone != null && betZone.EndDate < now)
            {
                historicBets.Add(bet);
            }
        }

        // Obtener PriceBets históricos (end_date < ahora)
        var allPriceBets = await _unitOfWork.PriceBets.GetUserPriceBetsAsync(request.UserId, includeArchived: true, cancellationToken);
        var historicPriceBets = allPriceBets.Where(pb => pb.EndDate < now).ToList();

        var allPriceBetsUSD = await _unitOfWork.PriceBetsUSD.GetUserPriceBetsAsync(request.UserId, includeArchived: true, cancellationToken);
        var historicPriceBetsUSD = allPriceBetsUSD.Where(pb => pb.EndDate < now).ToList();

        if (historicBets.Count == 0 && historicPriceBets.Count == 0 && historicPriceBetsUSD.Count == 0)
            return 0;

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            int deletedCount = 0;
            
            // Eliminar apuestas normales históricas
            foreach (var bet in historicBets)
            {
                _unitOfWork.Bets.Remove(bet);
                deletedCount++;
            }

            // Eliminar PriceBets históricos EUR
            foreach (var priceBet in historicPriceBets)
            {
                _unitOfWork.PriceBets.Remove(priceBet);
                deletedCount++;
            }

            // Eliminar PriceBets históricos USD
            foreach (var priceBet in historicPriceBetsUSD)
            {
                _unitOfWork.PriceBetsUSD.Remove(priceBet);
                deletedCount++;
            }

            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);
            return deletedCount;
        }
        catch
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            throw;
        }
    }
}
