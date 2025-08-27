using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;

namespace BetsTrading_Service.Models;

[Index(nameof(TransactionId), IsUnique = true)]
[Index(nameof(UserId))]
public class RewardTransaction
{
  [Key]
  public Guid Id { get; set; } = Guid.NewGuid();
  
  [Required, MaxLength(128)]
  public string TransactionId { get; set; } = default!;

  [Required, MaxLength(128)]
  public string UserId { get; set; } = default!;
  
  public decimal Coins { get; set; }
  
  [MaxLength(128)]
  public string? AdUnitId { get; set; }

  [MaxLength(64)]
  public string? RewardItem { get; set; }

  public double? RewardAmountRaw { get; set; }

  public int? SsvKeyId { get; set; }

  public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
  
  public string? RawQuery { get; set; }
}
