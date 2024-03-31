using System.ComponentModel.DataAnnotations;
using static System.Net.WebRequestMethods;

namespace BetsTrading_Service.Requests
{
  public class googleSignRequest
  {

    [Required]
    [StringLength(100, MinimumLength = 21)]
    public string? id { get; set; }

    public DateTime? birthday { get; set; }
   
    [Required]
    [StringLength(100, MinimumLength = 1)]
    public string? displayName { get; set; }
   
    [Required]
    [StringLength(100, MinimumLength = 4)]
    public string? email { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 15)]
    public string? photoUrl { get; set; }

    public string? serverAuthCode { get; set; }


  }
}

