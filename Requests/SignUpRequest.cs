using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class SignUpRequest
  {
    [Required]
    public string? IdCard { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 3)]
    public string? FullName { get; set; }

    [Required]
    [StringLength(100, MinimumLength = 6)]
    public string? Password { get; set; }

    public string? Address { get; set; }

    [Required]
    public string? Country { get; set; }

    public string? Gender { get; set; }

    [Required]
    [EmailAddress]
    public string? Email { get; set; }

    [Required]
    public DateTime Birthday { get; set; }

    // REMOVE FROM HERE IN FUTURE
    public string? CreditCard { get; set; }

    [Required]
    [StringLength(50, MinimumLength = 3)]
    public string? Username { get; set; }

    public string? ProfilePic { get; set; }

  }

}
