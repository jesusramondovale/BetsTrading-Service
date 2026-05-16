namespace BetsTrading.API.Health;

internal sealed class DeleteAccountRequestDto
{
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ConfirmFullname { get; set; }
}
