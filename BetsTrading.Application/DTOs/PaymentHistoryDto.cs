using System.Text.Json.Serialization;

namespace BetsTrading.Application.DTOs;

public class PaymentHistoryDto
{
    [JsonPropertyName("id")]
    public Guid Id { get; set; }
    
    [JsonPropertyName("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [JsonPropertyName("payment_intent_id")]
    public string PaymentIntentId { get; set; } = string.Empty;
    
    [JsonPropertyName("coins")]
    public double Coins { get; set; }
    
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = string.Empty;
    
    [JsonPropertyName("amount")]
    public double Amount { get; set; }
    
    [JsonPropertyName("executed_at")]
    public DateTime? ExecutedAt { get; set; }
    
    [JsonPropertyName("is_paid")]
    public bool IsPaid { get; set; }
    
    [JsonPropertyName("payment_method")]
    public string PaymentMethod { get; set; } = string.Empty;
}
