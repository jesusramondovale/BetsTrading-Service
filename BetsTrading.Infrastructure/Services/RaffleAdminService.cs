using BetsTrading.Application.DTOs;
using BetsTrading.Application.Interfaces;
using BetsTrading.Domain;
using BetsTrading.Domain.Entities;
using BetsTrading.Domain.Interfaces;

namespace BetsTrading.Infrastructure.Services;

public class RaffleAdminService : IRaffleAdminService
{
    private readonly IUnitOfWork _unitOfWork;

    public RaffleAdminService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<IReadOnlyList<AdminRaffleItemDto>> GetItemsAsync(CancellationToken cancellationToken = default)
    {
        await _unitOfWork.RaffleItems.EnsureDefaultItemsAsync(cancellationToken);
        var items = await _unitOfWork.RaffleItems.GetAllAsync(cancellationToken);
        return items
            .OrderBy(i => i.Id)
            .Take(RaffleSchedule.RaffleItemCount)
            .Select(i => new AdminRaffleItemDto
            {
                Id = i.Id,
                Name = i.Name,
                ShortName = i.ShortName,
                Coins = i.Coins,
                RaffleDate = i.RaffleDate,
                Icon = i.Icon
            })
            .ToList();
    }

    public async Task SaveItemsAsync(IReadOnlyList<AdminRaffleItemDto> items, CancellationToken cancellationToken = default)
    {
        if (items.Count != RaffleSchedule.RaffleItemCount)
            throw new ArgumentException($"Exactly {RaffleSchedule.RaffleItemCount} raffle items are required.");

        var ids = items.Select(i => i.Id).OrderBy(x => x).ToArray();
        for (var expected = 1; expected <= RaffleSchedule.RaffleItemCount; expected++)
        {
            if (ids[expected - 1] != expected)
                throw new ArgumentException("Raffle item ids must be 1 through 6.");
        }

        await _unitOfWork.RaffleItems.EnsureDefaultItemsAsync(cancellationToken);

        foreach (var dto in items)
        {
            if (string.IsNullOrWhiteSpace(dto.Name) || string.IsNullOrWhiteSpace(dto.ShortName))
                throw new ArgumentException($"Item {dto.Id}: name and shortName are required.");
            if (dto.Coins < 1)
                throw new ArgumentException($"Item {dto.Id}: coins must be at least 1.");

            var entity = await _unitOfWork.RaffleItems.GetByIdAsync(dto.Id, cancellationToken);
            if (entity == null)
            {
                entity = new RaffleItem(
                    dto.Id,
                    dto.Name.Trim(),
                    dto.ShortName.Trim(),
                    dto.Coins,
                    dto.RaffleDate,
                    dto.Icon ?? string.Empty,
                    0);
                await _unitOfWork.RaffleItems.AddAsync(entity, cancellationToken);
                continue;
            }

            entity.UpdateFromAdmin(
                dto.Name,
                dto.ShortName,
                dto.Coins,
                dto.RaffleDate,
                string.IsNullOrWhiteSpace(dto.Icon) ? null : dto.Icon);
            _unitOfWork.RaffleItems.Update(entity);
        }

        await _unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
