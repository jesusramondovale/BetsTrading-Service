using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class BetRepository : Repository<Bet>, IBetRepository
{
    public BetRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<IEnumerable<Bet>> GetUserBetsAsync(string userId, bool includeArchived = false, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(b => b.UserId == userId);

        if (!includeArchived)
        {
            query = query.Where(b => !b.Archived);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public new async Task<Bet?> GetByIdAsync(int id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FindAsync(new object[] { id }, cancellationToken);
    }

    public async Task<Bet?> GetBetByIdAsync(int betId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(b => b.Id == betId, cancellationToken);
    }

    public async Task<IEnumerable<Bet>> GetBetsByBetZoneIdsAsync(IEnumerable<int> betZoneIds, bool includeFinished = false, CancellationToken cancellationToken = default)
    {
        var query = _dbSet.Where(b => betZoneIds.Contains(b.BetZoneId));
        
        if (!includeFinished)
        {
            query = query.Where(b => !b.Finished);
        }

        return await query.ToListAsync(cancellationToken);
    }

    public async Task<Dictionary<int, double>> GetBetVolumesByZoneIdsAsync(IEnumerable<int> betZoneIds, CancellationToken cancellationToken = default)
    {
        var volumes = await _dbSet
            .Where(b => betZoneIds.Contains(b.BetZoneId))
            .GroupBy(b => b.BetZoneId)
            .Select(g => new { ZoneId = g.Key, Volume = g.Sum(b => b.BetAmount) })
            .ToListAsync(cancellationToken);

        return volumes.ToDictionary(v => v.ZoneId, v => v.Volume);
    }

    public async Task<int> InsertBetWithRawSqlAsync(Bet bet, CancellationToken cancellationToken = default)
    {
        // Deshabilitar temporalmente las restricciones de clave foránea
        // La clave foránea bet_zone solo valida contra BetZones, pero puede referenciar BetZonesUSD también
        await _context.Database.ExecuteSqlRawAsync(
            "SET session_replication_role = 'replica'", cancellationToken);
        
        // Ejecutar INSERT directamente
        // Crear array de parámetros explícitamente para evitar que CancellationToken se interprete como parámetro SQL
        object[] parameters = new object[]
        {
            bet.UserId,
            bet.Ticker,
            bet.BetAmount,
            bet.OriginValue,
            bet.OriginOdds,
            bet.TargetWon,
            bet.Finished,
            bet.Paid,
            bet.BetZoneId,
            bet.Archived
        };
        
        await _context.Database.ExecuteSqlRawAsync(
            @"INSERT INTO ""BetsTrading"".""Bets"" (user_id, ticker, bet_amount, origin_value, origin_odds, target_won, finished, paid, bet_zone, archived)
              VALUES ({0}, {1}, {2}, {3}, {4}, {5}, {6}, {7}, {8}, {9})",
            parameters,
            cancellationToken);
        
        // Rehabilitar las restricciones
        await _context.Database.ExecuteSqlRawAsync(
            "SET session_replication_role = 'origin'", cancellationToken);
        
        // Consultar el último ID insertado para este usuario, ticker y bet_zone
        var lastBet = await _dbSet
            .Where(b => b.UserId == bet.UserId && b.BetZoneId == bet.BetZoneId && b.Ticker == bet.Ticker)
            .OrderByDescending(b => b.Id)
            .FirstOrDefaultAsync(cancellationToken);
        
        return lastBet?.Id ?? 0;
    }
}
