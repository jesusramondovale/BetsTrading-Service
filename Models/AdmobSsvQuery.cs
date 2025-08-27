using Microsoft.AspNetCore.Mvc;

namespace BetsTrading_Service.Models;

public class AdmobSsvQuery
{
  [FromQuery(Name = "reward_amount")]
  public string? RewardAmount { get; set; }

  [FromQuery(Name = "reward_item")]
  public string? RewardItem { get; set; }

  [FromQuery(Name = "transaction_id")]
  public string? TransactionId { get; set; }

  [FromQuery(Name = "user_id")]
  public string? UserId { get; set; }

  [FromQuery(Name = "custom_data")]
  public string? CustomData { get; set; } // tu nonce

  [FromQuery(Name = "ad_unit")]
  public string? AdUnit { get; set; }

  [FromQuery(Name = "signature")]
  public string? Signature { get; set; }

  [FromQuery(Name = "key_id")]
  public int? KeyId { get; set; }

  // Cualquier otro parámetro que quieras capturar
}
