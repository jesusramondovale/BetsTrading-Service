using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IWithdrawalMethodRepository : IRepository<WithdrawalMethod>
{
    Task<IEnumerable<WithdrawalMethod>> GetByUserIdAsync(string userId, CancellationToken cancellationToken = default);
    Task<WithdrawalMethod?> GetByUserIdAndLabelAsync(string userId, string label, CancellationToken cancellationToken = default);
    Task<WithdrawalMethod?> GetByUserIdTypeAndLabelAsync(string userId, string type, string label, CancellationToken cancellationToken = default);
}
