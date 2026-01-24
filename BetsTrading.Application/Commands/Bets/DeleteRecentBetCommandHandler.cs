using MediatR;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Application.Commands.Bets;

public class DeleteRecentBetCommandHandler : IRequestHandler<DeleteRecentBetCommand, bool>
{
    private readonly IUnitOfWork _unitOfWork;

    public DeleteRecentBetCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteRecentBetCommand request, CancellationToken cancellationToken)
    {
        var bet = await _unitOfWork.Bets.GetByIdAsync(request.BetId, cancellationToken);
        if (bet == null)
            return false;

        bet.Archive();

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            _unitOfWork.Bets.Update(bet);
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
