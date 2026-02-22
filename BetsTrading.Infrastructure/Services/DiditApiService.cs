using System.Text;
using System.Text.Json;
using BetsTrading.Application.Interfaces;
using Microsoft.Extensions.Http;

namespace BetsTrading.Infrastructure.Services;

public class DiditApiService : IDiditApiService
{
    private readonly string _apiKey;
    private readonly IHttpClientFactory _httpClientFactory;
    private const string BaseUrl = "https://verification.didit.me/v2";

    public DiditApiService(IHttpClientFactory httpClientFactory)
    {
        _apiKey = Environment.GetEnvironmentVariable("DIDIT_API_KEY") ?? "";
        _httpClientFactory = httpClientFactory;
    }

    public async Task<DiditSessionResponse> CreateSessionAsync(string workflowId, string vendorData, string callbackUrl, CancellationToken cancellationToken = default)
    {
        var http = _httpClientFactory.CreateClient("Didit");
        var payload = new
        {
            workflow_id = workflowId,
            vendor_data = vendorData,
            callback = callbackUrl
        };

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/session/");
        request.Headers.Add("x-api-key", _apiKey);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        var response = await http.SendAsync(request, cancellationToken);

        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"Didit API error: {response.StatusCode} - {body}");
        }

        var json = JsonSerializer.Deserialize<JsonElement>(body);
        var sessionId = json.TryGetProperty("session_id", out var idProp) ? idProp.GetString() : null;

        return new DiditSessionResponse
        {
            SessionId = sessionId,
            RawResponse = json
        };
    }

    public async Task<DiditDecisionResponse?> GetSessionDecisionAsync(string sessionId, CancellationToken cancellationToken = default)
    {
        var http = _httpClientFactory.CreateClient("Didit");
        using var request = new HttpRequestMessage(HttpMethod.Get, $"{BaseUrl}/session/{sessionId}/decision");
        request.Headers.Add("x-api-key", _apiKey);

        var response = await http.SendAsync(request, cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = JsonSerializer.Deserialize<JsonElement>(body);

        if (!json.TryGetProperty("id_verification", out var idVer))
        {
            return null;
        }

        var idVerification = new DiditIdVerification();

        if (idVer.TryGetProperty("age", out var age))
        {
            idVerification.Age = age.GetInt16();
        }

        if (idVer.TryGetProperty("full_name", out var fullName))
        {
            idVerification.FullName = fullName.GetString();
        }

        if (idVer.TryGetProperty("issuing_state_name", out var country))
        {
            idVerification.IssuingStateName = country.GetString();
        }

        if (idVer.TryGetProperty("date_of_birth", out var dobProp))
        {
            var dobStr = dobProp.GetString();
            if (!string.IsNullOrEmpty(dobStr) && DateTime.TryParse(dobStr, out var dob))
            {
                idVerification.DateOfBirth = dob;
            }
        }

        return new DiditDecisionResponse
        {
            IdVerification = idVerification
        };
    }
}
