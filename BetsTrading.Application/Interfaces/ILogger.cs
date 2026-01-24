namespace BetsTrading.Application.Interfaces;

public interface IApplicationLogger
{
    void Information(string message, params object[] args);
    void Warning(string message, params object[] args);
    void Error(Exception? exception, string message, params object[] args);
    void Debug(string message, params object[] args);
    void Fatal(string message, params object[] args);
}
