using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;

namespace BetsTrading_Service.Models;

[Index(nameof(Nonce), IsUnique = true)]
[Index(nameof(UserId))]
public class RewardNonce
{
  [Key]
  public Guid Id { get; set; } = Guid.NewGuid();
  
  [Required, MaxLength(256)]
  public string Nonce { get; set; } = default!;
  
  [Required, MaxLength(128)]
  public string UserId { get; set; } = default!;
  
  [Required, MaxLength(128)]
  public string AdUnitId { get; set; } = default!;
  
  [MaxLength(64)]
  public string? Purpose { get; set; }
  
  public bool Used { get; set; } = false;
  
  public DateTime ExpiresAt { get; set; }
  
  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  
  public DateTime? UsedAt { get; set; }
}

public class RewardNonceRequest
{
  [JsonPropertyName("adUnitId")]
  public string AdUnitId { get; set; } = default!;
  [JsonPropertyName("purpose")]
  public string? Purpose { get; set; }
}

public class RewardNonceResponse
{
  public string Nonce { get; set; } = default!;
  public DateTime ExpiresAt { get; set; }
}

