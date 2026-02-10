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
            // Normalizar ticker para evitar duplicados por diferencias de mayúsculas/minúsculas
            var ticker = (request.Ticker ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(ticker))
            {
                return new ToggleFavoriteResult
                {
                    Success = false,
                    Message = "Ticker is required",
                    IsFavorite = false
                };
            }
            ticker = ticker.ToUpperInvariant();

            var existingFavorite = await _unitOfWork.Favorites.GetByUserIdAndTickerAsync(
                userId,
                ticker,
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
                // Add favorite (siempre guardamos en mayúsculas para consistencia)
                var newFavorite = new Favorite(
                    Guid.NewGuid().ToString(),
                    userId,
                    ticker);

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
