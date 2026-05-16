namespace BetsTrading.Application.Interfaces;

public interface IUserAccountDeletionService
{
    Task<DeleteUserAccountResult> DeleteAccountAsync(
        string emailOrUsername,
        string password,
        string confirmFullname,
        CancellationToken cancellationToken = default);
}

public class DeleteUserAccountResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? UserId { get; set; }
}
