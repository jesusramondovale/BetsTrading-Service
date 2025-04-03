using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class newBetRequest
  {
    [Required]
    [StringLength(100, MinimumLength = 20)]
    public string? user_id { get; set; }

    [Required]
    public string? ticker { get; set; }

    [Required]
    public double bet_amount { get; set; }

    [Required]
    public  double origin_value { get; set; }

    [Required]
    public int bet_zone { get; set; }

  }

  public class newPriceBetRequest
  {
    [Required]
    [StringLength(100, MinimumLength = 20)]
    public string? user_id { get; set; }

    [Required]
    public string? ticker { get; set; }

    [Required]
    public double price_bet { get; set; }

    [Required]
    public double margin { get; set; }

    [Required]
    public DateTime end_date { get; set; }


  }
}
