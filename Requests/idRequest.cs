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



  public class fcmTokenRequest
  {

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? user_id { get; set; }

    [Required]
    [StringLength(200, MinimumLength = 1)]
    public string? fcm_token { get; set; }

  }

  public class addCoinsRequest
  {

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? user_id { get; set; }

    [Required]
    public double? reward { get; set; }

  }

  public class idCardRequest
  {

    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? id { get; set; }

    public string? idCard { get; set; }

  }

}
