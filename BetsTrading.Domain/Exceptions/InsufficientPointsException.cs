namespace BetsTrading.Domain.Exceptions;

public class InsufficientPointsException : Exception
{
    public InsufficientPointsException() : base("Insufficient points to complete this operation")
    {
    }

    public InsufficientPointsException(string message) : base(message)
    {
    }
}
