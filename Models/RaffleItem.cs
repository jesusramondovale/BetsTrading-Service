namespace BetsTrading_Service.Models
{
  public class RaffleItem(int id, string name, string short_name, int coins, DateTimeOffset raffle_date, string icon)
  {
    public int id { get; private set; } = id;
    public string name { get; private set; } = name;
    public string short_name { get; private set; } = short_name;
    public int coins { get; set; } = coins;
    public DateTimeOffset raffle_date { get; private set; } = raffle_date;
    public string icon { get; private set; } = icon;
  }
}
