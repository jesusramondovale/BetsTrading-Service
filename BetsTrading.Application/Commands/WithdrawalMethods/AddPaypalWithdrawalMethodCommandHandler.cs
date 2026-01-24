using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Domain.Entities;
using System.Text.Json;

namespace BetsTrading.Application.Commands.WithdrawalMethods;

public class AddPaypalWithdrawalMethodCommandHandler : IRequestHandler<AddPaypalWithdrawalMethodCommand, AddWithdrawalMethodResult>
{
    private readonly IUnitOfWork _unitOfWork;

    public AddPaypalWithdrawalMethodCommandHandler(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<AddWithdrawalMethodResult> Handle(AddPaypalWithdrawalMethodCommand request, CancellationToken cancellationToken)
    {
        await _unitOfWork.BeginTransactionAsync(cancellationToken);

        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return new AddWithdrawalMethodResult { Success = false, Message = "User not found" };
            }

            var dataObj = new { email = request.Email?.Trim() };
            var jsonDoc = JsonDocument.Parse(JsonSerializer.Serialize(dataObj));

            var existing = await _unitOfWork.WithdrawalMethods.GetByUserIdTypeAndLabelAsync(
                request.UserId, "paypal", request.Label, cancellationToken);

            if (existing == null)
            {
                var entity = new WithdrawalMethod(
                    Guid.NewGuid(), request.UserId, "paypal", request.Label, jsonDoc,
                    user.IsVerified, DateTime.UtcNow, DateTime.UtcNow);

                await _unitOfWork.WithdrawalMethods.AddAsync(entity, cancellationToken);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new AddWithdrawalMethodResult
                {
                    Success = true, Id = entity.Id, Created = true, Verified = entity.Verified
                };
            }
            else
            {
                existing.Data = jsonDoc;
                existing.UpdatedAt = DateTime.UtcNow;
                _unitOfWork.WithdrawalMethods.Update(existing);
                await _unitOfWork.SaveChangesAsync(cancellationToken);
                await _unitOfWork.CommitTransactionAsync(cancellationToken);

                return new AddWithdrawalMethodResult
                {
                    Success = true, Id = existing.Id, Created = false, Verified = existing.Verified
                };
            }
        }
        catch (Exception ex)
        {
            await _unitOfWork.RollbackTransactionAsync(cancellationToken);
            return new AddWithdrawalMethodResult { Success = false, Message = $"Error: {ex.Message}" };
        }
    }
}
