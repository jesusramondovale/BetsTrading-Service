using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BetsTrading_Service.Requests
{
  public class addBankWithdrawalMethodRequest
  {
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }
    
    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    [JsonPropertyName("iban")]
    public string? Iban { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    [JsonPropertyName("holder")]
    public string? Holder { get; set; }

    [JsonPropertyName("bic")]
    public string? Bic { get; set; }

  }

  public class addPaypalWithdrawalMethodRequest
  {
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    [JsonPropertyName("email")]
    public string? Email { get; set; }
    
  }

  public class addCryptoWithdrawalMethodRequest
  {
    [Required]
    [StringLength(50, MinimumLength = 3)]
    [JsonPropertyName("user_id")]
    public string? UserId { get; set; }

    [JsonPropertyName("label")]
    public string? Label { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    [JsonPropertyName("network")]
    public string? Network { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    [JsonPropertyName("address")]
    public string? Address { get; set; }

    [JsonPropertyName("memo")]
    public string? Memo { get; set; }

  }

}
