using BetsTrading.Domain.Entities;

namespace BetsTrading.Domain.Interfaces;

public interface IUserRepository : IRepository<User>
{
    Task<User?> GetByIdAsync(string id, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default);
    Task<User?> GetByUsernameAsync(string username, CancellationToken cancellationToken = default);
    Task<User?> GetByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetTopUsersByPointsAsync(int limit, CancellationToken cancellationToken = default);
    Task<IEnumerable<User>> GetTopUsersByCountryAsync(string countryCode, int limit, CancellationToken cancellationToken = default);
}
