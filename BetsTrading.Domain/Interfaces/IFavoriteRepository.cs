using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IFavoriteRepository : IRepository<Favorite>
{
    Task<IEnumerable<Favorite>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<Favorite?> GetByUserIdAndTickerAsync(string userId, string ticker, CancellationToken cancellationToken = default);
}
