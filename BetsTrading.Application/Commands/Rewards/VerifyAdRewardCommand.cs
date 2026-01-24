using MediatR;

namespace BetsTrading.Application.Commands.Rewards;

public class VerifyAdRewardCommand : IRequest<VerifyAdRewardResult>
{
    public string UserId { get; set; } = string.Empty;
    public string TransactionId { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string? AdUnitId { get; set; }
    public string? RewardItem { get; set; }
    public double? RewardAmountRaw { get; set; }
    public int? SsvKeyId { get; set; }
    public string? RawQuery { get; set; }
    public string? Signature { get; set; }
    public string? KeyId { get; set; }
}

public class VerifyAdRewardResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
}
