using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;

namespace BetsTrading.Application.Commands.Favorites;

public class ToggleFavoriteCommandHandler : IRequestHandler<ToggleFavoriteCommand, ToggleFavoriteResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public ToggleFavoriteCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<ToggleFavoriteResult> Handle(ToggleFavoriteCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var userId = request.GetUserId();
            var existingFavorite = await _unitOfWork.Favorites.GetByUserIdAndTickerAsync(
                userId, 
                request.Ticker, 
                cancellationToken);

            if (existingFavorite != null)
            {
                // Remove favorite
                _unitOfWork.Favorites.Remove(existingFavorite);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new ToggleFavoriteResult
                {
                    Success = true,
                    Message = "Favorite removed",
                    IsFavorite = false
                };
            }
            else
            {
                // Add favorite
                var newFavorite = new Favorite(
                    Guid.NewGuid().ToString(),
                    userId,
                    request.Ticker);

                await _unitOfWork.Favorites.AddAsync(newFavorite, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new ToggleFavoriteResult
                {
                    Success = true,
                    Message = "Favorite added",
                    IsFavorite = true
                };
            }
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new ToggleFavoriteResult
            {
                Success = false,
                Message = $"Error: {ex.Message}",
                IsFavorite = false
            };
        }
    }
}
