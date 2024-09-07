using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class newFavoriteRequest
  {
    [Required]
    [StringLength(100, MinimumLength = 20)]
    public string? user_id { get; set; }

    [Required]
    public string? ticker{ get; set; }


  }
}