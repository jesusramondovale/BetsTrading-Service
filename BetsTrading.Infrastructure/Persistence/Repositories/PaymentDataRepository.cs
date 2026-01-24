using Microsoft.EntityFrameworkCore;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;

namespace BetsTrading.Infrastructure.Persistence.Repositories;

public class PaymentDataRepository : Repository<PaymentData>, IPaymentDataRepository
{
    public PaymentDataRepository(AppDbContext context) : base(context)
    {
    }

    public async Task<PaymentData?> GetByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default)
    {
        return await _dbSet.FirstOrDefaultAsync(p => p.PaymentIntentId == paymentIntentId, cancellationToken);
    }

    public async Task<IEnumerable<PaymentData>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default)
    {
        return await _dbSet
            .Where(p => p.UserId == userId)
            .OrderByDescending(p => p.ExecutedAt)
            .ToListAsync(cancellationToken);
    }
}
