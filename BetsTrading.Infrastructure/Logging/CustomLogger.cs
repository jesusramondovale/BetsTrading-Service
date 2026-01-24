using Serilog.Core;
using ILogger = Serilog.ILogger;

namespace BetsTrading.Infrastructure.Logging;

public interface ICustomLogger
{
    ILogger Log { get; }
}

public class CustomLogger : ICustomLogger
{
    private readonly Logger _logger;

    public ILogger Log => _logger;

    public CustomLogger(Logger logger)
    {
        _logger = logger;
    }
}
