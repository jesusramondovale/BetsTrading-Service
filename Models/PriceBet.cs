namespace BetsTrading_Service.Models
{
  public class PriceBet
  {
    public PriceBet(string user_id, string ticker, double price_bet, double margin, DateTime end_date)
    {
      this.user_id = user_id;
      this.ticker = ticker;
      this.price_bet = price_bet;
      this.paid = false;
      this.bet_date = DateTime.Now;
      this.end_date = end_date;
    }

    public int id { get; set; }
    public string user_id { get; set; }
    public string ticker { get; set; }
    public double price_bet { get; set; }
    public double margin { get; set; }
    public bool paid { get; set; }
    public DateTime bet_date {  get; set; }
    public DateTime end_date { get; set; }

  }
}

 