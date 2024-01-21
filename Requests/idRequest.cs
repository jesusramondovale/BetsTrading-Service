using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class idRequest
  {
    [Required]
    [StringLength(100, MinimumLength = 25)]
    public string? id { get; set; }


  }
}
