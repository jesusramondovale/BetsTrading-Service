using Microsoft.EntityFrameworkCore;
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
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }
}
