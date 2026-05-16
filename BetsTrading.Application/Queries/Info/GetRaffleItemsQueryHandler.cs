using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;

namespace BetsTrading.Application.Queries.Info;

public class GetRaffleItemsQueryHandler : IRequestHandler<GetRaffleItemsQuery, GetRaffleItemsResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetRaffleItemsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetRaffleItemsResult> Handle(GetRaffleItemsQuery request, CancellationToken cancellationToken)
    {
        var userExists = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (userExists == null)
        {
            return new GetRaffleItemsResult
            {
                Success = false,
                Message = "User not found"
            };
        }

        await _unitOfWork.RaffleItems.EnsureDefaultItemsAsync(cancellationToken);
        var items = await _unitOfWork.RaffleItems.GetAllAsync(cancellationToken);
        var itemList = items.OrderBy(r => r.Coins).ToList();

        if (itemList.Count == 0)
        {
            return new GetRaffleItemsResult
            {
                Success = false,
                Message = "No raffle items found"
            };
        }

        var participatedIds = await _unitOfWork.Raffles.GetParticipatedItemIdsForUserAsync(request.UserId, cancellationToken);

        var itemDtos = itemList.Select(r => new RaffleItemDto
        {
            Id = r.Id,
            Name = r.Name,
            ShortName = r.ShortName,
            Coins = r.Coins,
            RaffleDate = r.RaffleDate,
            Icon = r.Icon,
            Participants = r.Participants,
            AlreadyParticipated = participatedIds.Contains(r.Id)
        }).ToList();

        return new GetRaffleItemsResult
        {
            Success = true,
            Items = itemDtos
        };
    }
}
