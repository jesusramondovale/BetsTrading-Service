using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IPaymentDataRepository : IRepository<PaymentData>
{
    Task<PaymentData?> GetByPaymentIntentIdAsync(string paymentIntentId, CancellationToken cancellationToken = default);
    Task<IEnumerable<PaymentData>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
}
