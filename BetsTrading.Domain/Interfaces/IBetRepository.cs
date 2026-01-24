using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IBetRepository : IRepository<Bet>
{
    Task<Bet?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<IEnumerable<Bet>> GetUserBetsAsync(string userId, bool includeArchived = false, CancellationToken cancellationToken = default);
    Task<Bet?> GetBetByIdAsync(int betId, CancellationToken cancellationToken = default);
    Task<IEnumerable<Bet>> GetBetsByBetZoneIdsAsync(IEnumerable<int> betZoneIds, bool includeFinished = false, CancellationToken cancellationToken = default);
    Task<Dictionary<int, double>> GetBetVolumesByZoneIdsAsync(IEnumerable<int> betZoneIds, CancellationToken cancellationToken = default);
    Task<int> InsertBetWithRawSqlAsync(Bet bet, CancellationToken cancellationToken = default);
}
