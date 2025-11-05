namespace BetsTrading_Service.Models
{
  public class Raffle(long id, int item_id, string user_id, int quantity, DateTime raffleDate)
  {
    public long id { get; private set; } = id;
    public int item_id { get; private set; } = item_id;
    public string user_id { get; private set; } = user_id;
    public int quantity { get; private set; } = quantity;
    public DateTime raffleDate { get; private set; } = raffleDate;
  }
}
