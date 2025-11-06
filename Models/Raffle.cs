using System.ComponentModel.DataAnnotations.Schema;
using System.ComponentModel.DataAnnotations;

namespace BetsTrading_Service.Models
{
  public class Raffle
  {
    [Key]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public long id { get; private set; }
    public string item_id { get; init; }
    public string user_id { get; init; } = null!;
    public DateTime raffle_date { get; init; }

    private Raffle() { }

    public Raffle(string item_id, string user_id, DateTime raffleDate)
    {
      this.item_id = item_id;
      this.user_id = user_id;
      this.raffle_date = raffleDate;
    }
  }
}
