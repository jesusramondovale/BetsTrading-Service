using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IWithdrawalDataRepository : IRepository<WithdrawalData>
{
    Task<IEnumerable<WithdrawalData>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<IEnumerable<WithdrawalData>> GetPendingWithdrawalsAsync(CancellationToken cancellationToken = default);
}
