using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class RaffleRepository : Repository<Raffle>, IRaffleRepository
{
    public RaffleRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Raffle>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(r => r.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UserHasParticipatedAsync(string userId, int itemId, CancellationToken cancellationToken = default)
    {
        var itemKey = itemId.ToString();
        return await _dbSet.AnyAsync(
            r => r.UserId == userId && r.ItemId == itemKey,
            cancellationToken);
    }

    public async Task<HashSet<int>> GetParticipatedItemIdsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        var keys = await _dbSet
            .Where(r => r.UserId == userId)
            .Select(r => r.ItemId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var ids = new HashSet<int>();
        foreach (var key in keys)
        {
            if (int.TryParse(key, out var id))
                ids.Add(id);
        }

        return ids;
    }

    public async Task<IReadOnlyList<Raffle>> GetEntriesByItemIdAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var itemKey = itemId.ToString();
        return await _dbSet
            .Where(r => r.ItemId == itemKey)
            .ToListAsync(cancellationToken);
    }

    public async Task DeleteEntriesByItemIdAsync(int itemId, CancellationToken cancellationToken = default)
    {
        var itemKey = itemId.ToString();
        var entries = await _dbSet
            .Where(r => r.ItemId == itemKey)
            .ToListAsync(cancellationToken);
        if (entries.Count == 0)
            return;

        _dbSet.RemoveRange(entries);
    }
}
