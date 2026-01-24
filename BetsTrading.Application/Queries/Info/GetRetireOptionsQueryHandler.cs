using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.DTOs;
using System.Text.Json;

namespace BetsTrading.Application.Queries.Info;

public class GetRetireOptionsQueryHandler : IRequestHandler<GetRetireOptionsQuery, GetRetireOptionsResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public GetRetireOptionsQueryHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<GetRetireOptionsResult> Handle(GetRetireOptionsQuery request, CancellationToken cancellationToken)
    {
        var userExists = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
        if (userExists == null)
        {
            return new GetRetireOptionsResult
            {
                Success = false,
                Message = "User not found"
            };
        }

        var methods = await _unitOfWork.WithdrawalMethods.GetByUserIdAsync(request.UserId, cancellationToken);

        var methodDtos = methods.Select(w => new WithdrawalMethodDto
        {
            Id = w.Id,
            Type = w.Type,
            Label = w.Label,
            Verified = w.Verified,
            Data = JsonSerializer.Deserialize<object>(w.Data.RootElement.GetRawText())!,
            CreatedAt = w.CreatedAt,
            UpdatedAt = w.UpdatedAt
        }).ToList();

        return new GetRetireOptionsResult
        {
            Success = true,
            Options = methodDtos
        };
    }
}
