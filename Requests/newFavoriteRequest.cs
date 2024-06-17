using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Requests
{
  public class newFavoriteRequest
  {
    [Required]
    [StringLength(100, MinimumLength = 20)]
    public string? id { get; set; }

    [Required]
    public string? item_name{ get; set; }


  }
}