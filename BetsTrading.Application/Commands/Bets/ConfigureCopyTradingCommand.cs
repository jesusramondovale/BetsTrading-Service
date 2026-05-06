using MediatR;

namespace BetsTrading.Application.Commands.Bets;

public class ConfigureCopyTradingCommand : IRequest<ConfigureCopyTradingResult>
{
    public string FollowerUserId { get; set; } = string.Empty;
    public string Fcm { get; set; } = string.Empty;
    public string TargetUserId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
    public double CopyPercent { get; set; } = 50;
    public bool AutoAdjustByBalance { get; set; }
    public bool StopAfterOneLoss { get; set; }
}

public class ConfigureCopyTradingResult
{
    public bool Active { get; set; }
    public string Message { get; set; } = string.Empty;
}
