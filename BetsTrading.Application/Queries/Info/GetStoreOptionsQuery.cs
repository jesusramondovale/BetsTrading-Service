using MediatR;

namespace BetsTrading.Application.Queries.Info;

public class GetStoreOptionsQuery : IRequest<GetStoreOptionsResult>
{
    public string Currency { get; set; } = "EUR";
    public string Type { get; set; } = string.Empty;
}

public class GetStoreOptionsResult
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<Dictionary<string, object>> Options { get; set; } = new();
}
