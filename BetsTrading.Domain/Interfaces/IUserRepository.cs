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

    /// <summary>Invalida todas las sesiones activas (cierre forzado global).</summary>
    Task<int> InvalidateAllActiveSessionsAsync(CancellationToken cancellationToken = default);

    /// <summary>Tokens FCM distintos de usuarios con sesión activa (antes de invalidar).</summary>
    Task<IReadOnlyList<string>> GetActiveSessionFcmTokensAsync(CancellationToken cancellationToken = default);

    /// <summary>Deduce puntos de forma atómica si el saldo es suficiente.</summary>
    Task<bool> TryDeductPointsAsync(string id, double amount, CancellationToken cancellationToken = default);

    /// <summary>Añade puntos de forma atómica.</summary>
    Task<bool> TryAddPointsAsync(string id, double amount, CancellationToken cancellationToken = default);

    /// <summary>Quita del change tracker un usuario cargado (evita que SaveChanges revierta deducciones atómicas).</summary>
    void DetachTracked(string id);
}
