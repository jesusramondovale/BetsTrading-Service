using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class idRequest
  {
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? id { get; set; }

  }

  public class idCardRequest
  {

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? id { get; set; }

    public string? idCard { get; set; }


  }

}
