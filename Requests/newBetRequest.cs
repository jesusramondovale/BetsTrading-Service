using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace BetsTrading_Service.Requests
{
  public class newBetRequest
  {

    
    [Required]
    [StringLength(100, MinimumLength = 20)]
    [JsonPropertyName("user_id")]
    public string? user_id{ get; set; }

    [Required]
    [JsonPropertyName("fcm")]
    public string? fcm { get; set; }

    [Required]
    [JsonPropertyName("ticker")]
    public string? ticker { get; set; }

    [Required]
    [JsonPropertyName("bet_amount")]
    public double bet_amount { get; set; }

    [Required]
    [JsonPropertyName("origin_value")]
    public  double origin_value { get; set; }

    [Required]
    [JsonPropertyName("bet_zone")]
    public int bet_zone { get; set; }

  }

  public class newPriceBetRequest
  {
    [Required]
    [StringLength(100, MinimumLength = 20)]
    [JsonPropertyName("user_id")]
    public string? user_id { get; set; }

    [Required]
    [JsonPropertyName("fcm")]
    public string? fcm { get; set; }

    [Required]
    [JsonPropertyName("ticker")]
    public string? ticker { get; set; }

    [Required]
    [JsonPropertyName("price_bet")]
    public double price_bet { get; set; }

    [Required]
    [JsonPropertyName("margin")]
    public double margin { get; set; }

    [Required]
    [JsonPropertyName("end_date")]
    public DateTime end_date { get; set; }


  }
}
