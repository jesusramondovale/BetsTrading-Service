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

        var items = await _unitOfWork.RaffleItems.GetAllAsync(cancellationToken);

        if (!items.Any())
        {
            return new GetRaffleItemsResult
            {
                Success = false,
                Message = "No raffle items found"
            };
        }

        var itemDtos = items.OrderBy(r => r.Coins).Select(r => new RaffleItemDto
        {
            Id = r.Id,
            Name = r.Name,
            ShortName = r.ShortName,
            Coins = r.Coins,
            RaffleDate = r.RaffleDate,
            Icon = r.Icon,
            Participants = r.Participants
        }).ToList();

        return new GetRaffleItemsResult
        {
            Success = true,
            Items = itemDtos
        };
    }
}
