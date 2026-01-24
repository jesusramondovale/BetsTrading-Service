using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Bets;

public class DeleteRecentPriceBetCommandHandler : IRequestHandler<DeleteRecentPriceBetCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRecentPriceBetCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteRecentPriceBetCommand request, CancellationToken cancellationToken)
    {
        if (request.Currency == "EUR")
        {
            var priceBet = await _unitOfWork.PriceBets.GetByIdAsync(request.PriceBetId, cancellationToken);
            if (priceBet == null)
                return false;

            priceBet.Archive();

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                _unitOfWork.PriceBets.Update(priceBet);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return true;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
        else
        {
            var priceBet = await _unitOfWork.PriceBetsUSD.GetByIdAsync(request.PriceBetId, cancellationToken);
            if (priceBet == null)
                return false;

            priceBet.Archive();

            await _unitOfWork.BeginTransactionAsync(cancellationToken);
            try
            {
                _unitOfWork.PriceBetsUSD.Update(priceBet);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);
                return true;
            }
            catch
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                throw;
            }
        }
    }
}
