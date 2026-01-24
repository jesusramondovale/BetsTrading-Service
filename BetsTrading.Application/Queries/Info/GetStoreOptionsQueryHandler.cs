using MediatR;

namespace BetsTrading.Application.Queries.Info;

public class GetStoreOptionsQueryHandler : IRequestHandler<GetStoreOptionsQuery, GetStoreOptionsResult>
{
    public Task<GetStoreOptionsResult> Handle(GetStoreOptionsQuery request, CancellationToken cancellationToken)
    {
        // This handler is not used - logic moved to controller
        // to avoid IWebHostEnvironment dependency in Application layer
        return Task.FromResult(new GetStoreOptionsResult
        {
            Success = false,
            Message = "Handler not implemented - use controller directly"
        });
    }
}
