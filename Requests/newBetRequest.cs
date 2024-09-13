using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class newBetRequest
  {
    [Required]
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
}
