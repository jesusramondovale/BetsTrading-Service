using BetsTrading.Application.Interfaces;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Infrastructure.Persistence;
using BCrypt.Net;
using Microsoft.EntityFrameworkCore;

namespace BetsTrading.Infrastructure.Services;

public class UserAccountDeletionService : IUserAccountDeletionService
{
    private readonly AppDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationLogger _logger;

    public UserAccountDeletionService(
        AppDbContext context,
        IUnitOfWork unitOfWork,
        IApplicationLogger logger)
    {
        _context = context;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<DeleteUserAccountResult> DeleteAccountAsync(
        string emailOrUsername,
        string password,
        string confirmFullname,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(emailOrUsername))
            return Fail("Usuario o email requerido");

        if (string.IsNullOrWhiteSpace(password))
            return Fail("Contraseña requerida");

        if (string.IsNullOrWhiteSpace(confirmFullname))
            return Fail("Debes escribir el nombre completo del usuario para confirmar");

        var user = await _unitOfWork.Users.GetByEmailOrUsernameAsync(emailOrUsername.Trim(), cancellationToken);
        if (user == null)
            return Fail("Usuario no encontrado");

        if (!BCrypt.Net.BCrypt.Verify(password, user.Password))
        {
            _logger.Warning("[DeleteAccount] :: Invalid password for user {UserId}", user.Id);
            return Fail("Contraseña incorrecta");
        }

        if (!string.Equals(
                confirmFullname.Trim(),
                user.Fullname.Trim(),
                StringComparison.OrdinalIgnoreCase))
        {
            return Fail("El nombre escrito no coincide con el nombre completo de la cuenta");
        }

        await _unitOfWork.BeginTransactionAsync(cancellationToken);
        try
        {
            var userId = user.Id;
            var email = user.Email;

            var bets = await _context.Bets.Where(b => b.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var favorites = await _context.Favorites.Where(f => f.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var raffles = await _context.Raffles.Where(r => r.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var priceBets = await _context.PriceBets.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var priceBetsUsd = await _context.PriceBetsUSD.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var copySubs = await _context.CopyTradingSubscriptions
                .Where(s => s.FollowerUserId == userId || s.TargetUserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            var withdrawalMethods = await _context.WithdrawalMethods
                .Where(w => w.UserId == userId)
                .ExecuteDeleteAsync(cancellationToken);
            var payments = await _context.PaymentData.Where(p => p.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var withdrawals = await _context.WithdrawalData.Where(w => w.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var rewardNonces = await _context.RewardNonces.Where(r => r.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var rewardTx = await _context.RewardTransactions.Where(r => r.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var streaks = await _context.DailyLoginStreaks.Where(s => s.UserId == userId).ExecuteDeleteAsync(cancellationToken);
            var codes = await _context.VerificationCodes
                .Where(v => v.Email == email)
                .ExecuteDeleteAsync(cancellationToken);

            var userDeleted = await _context.Users.Where(u => u.Id == userId).ExecuteDeleteAsync(cancellationToken);
            if (userDeleted == 0)
            {
                await _unitOfWork.RollbackTransactionAsync(cancellationToken);
                return Fail("No se pudo eliminar el usuario");
            }

            await _unitOfWork.CommitTransactionAsync(cancellationToken);

            _logger.Information(
                "[DeleteAccount] :: Account deleted | userId={UserId} | bets={Bets} favs={Favs} raffles={Raffles} priceBets={PriceBets} priceBetsUsd={PriceBetsUsd} copy={Copy} wm={Wm}",
                userId,
                bets,
                favorites,
                raffles,
                priceBets,
                priceBetsUsd,
                copySubs,
                withdrawalMethods);

            return new DeleteUserAccountResult
            {
                Success = true,
                Message = "Tu cuenta ha sido eliminada correctamente",
                UserId = userId
            };
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            _logger.Error(ex, "[DeleteAccount] :: Failed for user lookup {EmailOrUsername}", emailOrUsername);
            return Fail($"Error al eliminar la cuenta: {ex.Message}");
        }
    }

    private static DeleteUserAccountResult Fail(string message) =>
        new() { Success = false, Message = message };
}
