using BetsTrading.Application.Interfaces;
using ILogger = Serilog.ILogger;

namespace BetsTrading.Infrastructure.Logging;

public class ApplicationLogger : IApplicationLogger
{
    private readonly ILogger _logger;

    public ApplicationLogger(ICustomLogger customLogger)
    {
        _logger = customLogger.Log;
    }

    public void Information(string message, params object[] args)
    {
        _logger.Information(message, args);
    }

    public void Warning(string message, params object[] args)
    {
        _logger.Warning(message, args);
    }

    public void Error(Exception? exception, string message, params object[] args)
    {
        if (exception != null)
            _logger.Error(exception, message, args);
        else
            _logger.Error(message, args);
    }

    public void Debug(string message, params object[] args)
    {
        _logger.Debug(message, args);
    }

    public void Fatal(string message, params object[] args)
    {
        _logger.Fatal(message, args);
    }
}
