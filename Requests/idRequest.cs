using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class idRequest
  {
    
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? id { get; set; }

  }

  public class integerIdRequest
  {

    [Required]
    public int? id { get; set; }

  }

  public class symbolWithTimeframe
  {

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? id { get; set; }

    [Required]
    public int? timeframe { get; set; } //1,2,4,24

  }




  public class tokenRequest
  {

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? user_id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string? token { get; set; }

  }

  public class storeOptionsrequest
  {

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? currency { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string? type { get; set; }

  }


  public class idCardRequest
  {

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? id { get; set; }

    public string? idCard { get; set; }

  }

}
