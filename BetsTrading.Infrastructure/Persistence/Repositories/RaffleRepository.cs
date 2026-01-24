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
}
