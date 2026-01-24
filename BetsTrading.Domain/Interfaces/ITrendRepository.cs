using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface ITrendRepository : IRepository<Trend>
{
    Task<Trend?> GetByTickerAsync(string ticker, CancellationToken cancellationToken = default);
    new Task<IEnumerable<Trend>> GetAllAsync(CancellationToken cancellationToken = default);
}
