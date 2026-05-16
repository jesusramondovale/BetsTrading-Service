using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Interfaces;

public interface IRaffleAdminService
{
    Task<IReadOnlyList<AdminRaffleItemDto>> GetItemsAsync(CancellationToken cancellationToken = default);
    Task SaveItemsAsync(IReadOnlyList<AdminRaffleItemDto> items, CancellationToken cancellationToken = default);
}
