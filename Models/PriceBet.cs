namespace BetsTrading_Service.Models
{
  public class PriceBet(string user_id, string ticker, double price_bet, int prize, double margin, DateTime end_date)
  {
    public int id { get; set; }
    public string user_id { get; set; } = user_id;
    public string ticker { get; set; } = ticker;
    public double price_bet { get; set; } = price_bet;
    public double margin { get; set; } = margin;
    public bool paid { get; set; } = false;
    public DateTime bet_date { get; set; } = DateTime.Now.ToUniversalTime();
    public DateTime end_date { get; set; } = end_date;
    public bool archived { get; set; } = false;
    public int prize { get; set; } = prize;

  }

  public class PriceBetUSD(string user_id, string ticker, double price_bet, int prize, double margin, DateTime end_date)
  {
    public int id { get; set; }
    public string user_id { get; set; } = user_id;
    public string ticker { get; set; } = ticker;
    public double price_bet { get; set; } = price_bet;
    public double margin { get; set; } = margin;
    public bool paid { get; set; } = false;
    public DateTime bet_date { get; set; } = DateTime.Now.ToUniversalTime();
    public DateTime end_date { get; set; } = end_date;
    public bool archived { get; set; } = false;
    public int prize { get; set; } = prize;

  }


}

 