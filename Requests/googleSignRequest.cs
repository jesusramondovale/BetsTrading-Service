using System.ComponentModel.DataAnnotations;
using static System.Net.WebRequestMethods;

namespace BetsTrading_Service.Requests
{
  public class googleSignRequest
  {

    [Required]
    public string? id { get; set; }

    [Required]
    public string? fcm { get; set; }

    public DateTime? birthday { get; set; }

    [Required]
    public string? country { get; set; }

    [Required]
    public string? displayName { get; set; }

    [Required]
    public string? email { get; set; }

    [Required]
    public string? photoUrl { get; set; }

    public string? serverAuthCode { get; set; }


  }
}

