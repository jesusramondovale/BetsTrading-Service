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
}
