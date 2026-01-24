using MediatR;
using BetsTrading.Application.DTOs;
using System.Text.Json.Serialization;

namespace BetsTrading.Application.Queries.FinancialAssets;

public class GetFinancialAssetsQuery : IRequest<IEnumerable<FinancialAssetDto>>
{
}

public class GetFinancialAssetsByGroupQuery : IRequest<IEnumerable<FinancialAssetDto>>
{
    // Support both "Group" (PascalCase) and "id" (snake_case from Flutter client)
    [JsonPropertyName("Group")]
    public string? Group { get; set; }
    
    [JsonPropertyName("id")]
    [System.Text.Json.Serialization.JsonIgnore(Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }
    
    // Support both "Currency" (PascalCase) and "currency" (snake_case from Flutter client)
    // Use "currency" as the JSON property name (client format) and map to Currency internally
    [JsonPropertyName("currency")]
    public string? CurrencyJson { get; set; }
    
    // Internal property for PascalCase (will be set manually if needed)
    [System.Text.Json.Serialization.JsonIgnore]
    public string? Currency { get; set; }
    
    // Computed property that returns the actual group from either property
    public string GetGroup()
    {
        if (!string.IsNullOrEmpty(Group))
            return Group;
        if (!string.IsNullOrEmpty(Id))
            return Id;
        return string.Empty;
    }
    
    // Computed property that returns the actual currency from either property
    public string GetCurrency()
    {
        // First check the PascalCase property (set manually)
        if (!string.IsNullOrEmpty(Currency))
            return Currency;
        // Then check the JSON property (from client)
        if (!string.IsNullOrEmpty(CurrencyJson))
            return CurrencyJson;
        return "EUR"; // Default
    }
}

public class GetFinancialAssetsByCountryQuery : IRequest<IEnumerable<FinancialAssetDto>>
{
    public string Country { get; set; } = string.Empty;
}
