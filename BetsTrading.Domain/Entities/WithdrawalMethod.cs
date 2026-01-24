using System.Text.Json;

namespace BetsTrading.Domain.Entities;

public class WithdrawalMethod
{
    private WithdrawalMethod() { }

    public WithdrawalMethod(Guid id, string? userId, string type, string label, JsonDocument data, bool verified, DateTime createdAt, DateTime updatedAt)
    {
        Id = id;
        UserId = userId;
        Type = type;
        Label = label;
        Data = data;
        Verified = verified;
        CreatedAt = createdAt;
        UpdatedAt = updatedAt;
    }

    public Guid Id { get; set; }
    public string? UserId { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public JsonDocument Data { get; set; } = JsonDocument.Parse("{}");
    public bool Verified { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
