using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class idRequest
  {
    
    [Required]
    [StringLength(100, MinimumLength = 21)]
    public string? id { get; set; }


  }
}
