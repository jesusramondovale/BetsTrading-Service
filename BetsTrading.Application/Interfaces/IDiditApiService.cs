namespace BetsTrading.Application.Interfaces;

public interface IDiditApiService
{
    Task<DiditSessionResponse> CreateSessionAsync(string workflowId, string vendorData, string callbackUrl, CancellationToken cancellationToken = default);
    Task<DiditDecisionResponse?> GetSessionDecisionAsync(string sessionId, CancellationToken cancellationToken = default);
}

public class DiditSessionResponse
{
    public string? SessionId { get; set; }
    public System.Text.Json.JsonElement? RawResponse { get; set; }
}

public class DiditDecisionResponse
{
    public DiditIdVerification? IdVerification { get; set; }
}

public class DiditIdVerification
{
    public short? Age { get; set; }
    public string? FullName { get; set; }
    public string? IssuingStateName { get; set; }
    public DateTime? DateOfBirth { get; set; }
}
