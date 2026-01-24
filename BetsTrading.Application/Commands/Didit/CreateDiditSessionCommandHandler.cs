using MediatR;
using BetsTrading.Domain.Interfaces;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Application.Commands.Didit;

public class CreateDiditSessionCommandHandler : IRequestHandler<CreateDiditSessionCommand, CreateDiditSessionResult>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IDiditApiService _diditApiService;
    private readonly IApplicationLogger _logger;

    public CreateDiditSessionCommandHandler(
        IUnitOfWork unitOfWork,
        IDiditApiService diditApiService,
        IApplicationLogger logger)
    {
        _unitOfWork = unitOfWork;
        _diditApiService = diditApiService;
        _logger = logger;
    }

    public async Task<CreateDiditSessionResult> Handle(CreateDiditSessionCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _unitOfWork.Users.GetByIdAsync(request.UserId, cancellationToken);
            if (user == null)
            {
                return new CreateDiditSessionResult
                {
                    Success = false,
                    Message = "User not found"
                };
            }

            var workflowId = Environment.GetEnvironmentVariable("DIDIT_WORKFLOW_ID") ?? "";
            var callbackUrl = Environment.GetEnvironmentVariable("DIDIT_CALLBACK_URL") 
                ?? "https://api.betstrading.online/api/Didit/Webhook";

            var response = await _diditApiService.CreateSessionAsync(
                workflowId,
                user.Id,
                callbackUrl,
                cancellationToken);

            if (!string.IsNullOrEmpty(response.SessionId))
            {
                user.UpdateDiditSessionId(response.SessionId);
                await _unitOfWork.SaveChangesAsync(cancellationToken);

                _logger.Information("[DIDIT] :: Session {0} created for user {1}", response.SessionId, request.UserId);
            }

            return new CreateDiditSessionResult
            {
                Success = true,
                SessionId = response.SessionId,
                Response = response.RawResponse
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "[DIDIT] :: Exception creating session for user {0}", request.UserId);
            return new CreateDiditSessionResult
            {
                Success = false,
                Message = "Internal server error"
            };
        }
    }
}
