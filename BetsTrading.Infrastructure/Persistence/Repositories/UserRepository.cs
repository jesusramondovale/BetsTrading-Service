using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class UserRepository : Repository<User>, IUserRepository
{
    public UserRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public async Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(u => u.Username == username, cancellationToken);
    }

    public async Task<User?> GetByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(
            u => u.Email == emailOrUsername || u.Username == emailOrUsername, 
            cancellationToken);
    }

    public async Task<IEnumerable<User>> GetTopUsersByPointsAsync(int limit, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .OrderByDescending(u => u.Points)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IEnumerable<User>> GetTopUsersByCountryAsync(string countryCode, int limit, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(u => u.Country == countryCode)
            .OrderByDescending(u => u.Points)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<int> InvalidateAllActiveSessionsAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .Where(u => u.IsActive && u.TokenExpiration > now)
            .ExecuteUpdateAsync(
                s => s
                    .SetProperty(u => u.IsActive, false)
                    .SetProperty(u => u.TokenExpiration, now)
                    .SetProperty(u => u.LastSession, now),
                cancellationToken);
    }

    public async Task<IReadOnlyList<string>> GetActiveSessionFcmTokensAsync(CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        return await _dbSet
            .AsNoTracking()
            .Where(u =>
                u.IsActive &&
                u.TokenExpiration > now &&
                u.Fcm != null &&
                u.Fcm != "" &&
                u.Fcm != "-")
            .Select(u => u.Fcm)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> TryDeductPointsAsync(string id, double amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            return false;

        var rows = await _dbSet
            .Where(u => u.Id == id && u.Points >= amount)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.Points, u => u.Points - amount),
                cancellationToken);
        return rows > 0;
    }

    public async Task<bool> TryAddPointsAsync(string id, double amount, CancellationToken cancellationToken = default)
    {
        if (amount <= 0)
            return false;

        var rows = await _dbSet
            .Where(u => u.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(u => u.Points, u => u.Points + amount),
                cancellationToken);
        return rows > 0;
    }
}
