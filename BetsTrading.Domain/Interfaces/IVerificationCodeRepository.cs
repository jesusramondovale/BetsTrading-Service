using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IVerificationCodeRepository : IRepository<VerificationCode>
{
    Task<VerificationCode?> GetByEmailAndCodeAsync(string email, string code, CancellationToken cancellationToken = default);
    Task<IEnumerable<VerificationCode>> GetUnverifiedByEmailAsync(string email, CancellationToken cancellationToken = default);
}
