using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class VerificationCodeRepository : Repository<VerificationCode>, IVerificationCodeRepository
{
    public VerificationCodeRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<VerificationCode?> GetByEmailAndCodeAsync(string email, string code, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .FirstOrDefaultAsync(v => v.Email == email && v.Code == code && !v.Verified && v.ExpiresAt > DateTime.UtcNow, cancellationToken);
    }

    public async Task<IEnumerable<VerificationCode>> GetUnverifiedByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(v => v.Email == email && !v.Verified)
            .ToListAsync(cancellationToken);
    }
}
