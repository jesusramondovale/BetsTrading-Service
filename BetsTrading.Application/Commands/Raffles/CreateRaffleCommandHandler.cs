using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;

namespace BetsTrading.Application.Commands.Raffles;

public class CreateRaffleCommandHandler : IRequestHandler<CreateRaffleCommand, CreateRaffleResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public CreateRaffleCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<CreateRaffleResult> Handle(CreateRaffleCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return new CreateRaffleResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            if (!int.TryParse(request.ItemToken, out var itemId))
            {
                return new CreateRaffleResult
                {
                    Success = false,
                    Message = "Invalid item token"
                };
            }

            var raffleItem = await _unitOfWork.RaffleItems.GetByIdAsync(itemId, cancellationToken);
            if (raffleItem == null)
            {
                return new CreateRaffleResult
                {
                    Success = false,
                    Message = "Raffle item not found"
                };
            }

            if (user.Points < raffleItem.Coins)
            {
                return new CreateRaffleResult
                {
                    Success = false,
                    Message = "Not enough points"
                };
            }

            // Deduct points
            user.DeductPoints(raffleItem.Coins);

            // Increment participants - RaffleItem has setter for Participants
            raffleItem.Participants++;
            _unitOfWork.RaffleItems.Update(raffleItem);

            // Create raffle
            var raffle = new Raffle(
                raffleItem.Id.ToString(),
                request.UserId,
                DateTime.UtcNow);

            await _unitOfWork.Raffles.AddAsync(raffle, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            return new CreateRaffleResult
            {
                Success = true,
                RaffleId = raffle.Id
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new CreateRaffleResult
            {
                Success = false,
                Message = $"Error: {ex.Message}"
            };
        }
    }
}
