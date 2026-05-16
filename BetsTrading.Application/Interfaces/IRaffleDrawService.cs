namespace BetsTrading.Application.Interfaces;

public interface IRaffleDrawService
{
    Task ProcessDueRafflesAsync(CancellationToken cancellationToken = default);
}
