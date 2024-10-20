using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class LoginRequest
  {
    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string? Username { get; set; }

    
    [StringLength(100, MinimumLength = 3)]
    public string? Password { get; set; }
  }
}
