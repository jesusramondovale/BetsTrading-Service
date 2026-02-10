using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class FavoriteRepository : Repository<Favorite>, IFavoriteRepository
{
    public FavoriteRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Favorite>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _context.Set<Favorite>()
            .Where(f => f.UserId == userId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Favorite?> GetByUserIdAndTickerAsync(string userId, string ticker, CancellationToken cancellationToken = default)
    {
        // Comparación insensible a mayúsculas para que añadir/quitar funcione
        // aunque el cliente envíe "aapl" o "AAPL" (el handler ya normaliza a mayúsculas)
        var tickerUpper = (ticker ?? string.Empty).Trim().ToUpperInvariant();
        return await _context.Set<Favorite>()
            .FirstOrDefaultAsync(f => f.UserId == userId && f.Ticker.ToUpper() == tickerUpper, cancellationToken);
    }
}
