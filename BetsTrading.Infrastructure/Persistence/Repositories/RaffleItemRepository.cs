using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class RaffleItemRepository : Repository<RaffleItem>, IRaffleItemRepository
{
    public RaffleItemRepository(AppDbContext context) : base(context)
    {
    }

    public new async Task<RaffleItem?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<RaffleItem>> GetDueItemsAsync(DateTimeOffset utcNow, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(r => r.RaffleDate <= utcNow)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task EnsureDefaultItemsAsync(CancellationToken cancellationToken = default)
    {
        var utcNow = DateTime.UtcNow;
        var existing = await _dbSet.Select(r => r.Id).ToListAsync(cancellationToken);
        var existingSet = existing.ToHashSet();

        for (var id = 1; id <= RaffleSchedule.RaffleItemCount; id++)
        {
            if (existingSet.Contains(id))
                continue;

            await _dbSet.AddAsync(RaffleItem.CreateDefault(id, utcNow), cancellationToken);
        }

        if (existing.Count < RaffleSchedule.RaffleItemCount)
            await _context.SaveChangesAsync(cancellationToken);
    }
}
