namespace BetsTrading.Domain.Exceptions;

public class BetException : Exception
{
    public BetException(string message) : base(message)
    {
    }
}
