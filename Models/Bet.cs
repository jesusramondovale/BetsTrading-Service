namespace BetsTrading_Service.Models
{
  public class Bet
  {

    // Full constructor
    public Bet(int id, string user_id , string ticker,
                      double bet_amount, double origin_value, double current_value,
                      double target_value, double target_margin, DateTime target_date,
                      double target_odds, bool target_won, string icon_path, int type, int date_margin) 
    
    {
      this.id = id;
      this.user_id = user_id;
      this.ticker = ticker;
      this.bet_amount = bet_amount;
      this.origin_value = origin_value;
      this.current_value = current_value;
      this.target_value = target_value;
      this.target_margin = target_margin;
      this.target_date = target_date;
      this.target_odds = target_odds;
      this.target_won = target_won;
      this.icon_path = icon_path;
      this.type = type;
      this.date_margin = date_margin;
    }

    public Bet(int id, string user_id, string ticker,
                     double bet_amount, double origin_value, double current_value,
                     double target_value, double target_margin, DateTime target_date,
                     double target_odds, string icon_path)

    {
      this.id = id;
      this.user_id = user_id;
      this.ticker = ticker;
      this.bet_amount = bet_amount;
      this.origin_value = origin_value;
      this.current_value = current_value;
      this.target_value = target_value;
      this.target_margin = target_margin;
      this.target_date = target_date;
      this.target_odds = target_odds;
      this.icon_path = icon_path;
      this.target_won = false;
      
    }


    public int id { get; private set; }
    public string user_id { get; private set; }
    public string ticker { get; private set; }
    public double bet_amount{ get; private set; }
    public double origin_value { get; private set; }
    public double current_value { get; set; }
    public double target_value { get; private set; }
    public double target_margin { get; private set; }
    public DateTime target_date { get; private set; }
    public double target_odds { get; private set; }
    public bool target_won { get; set; }
    public string icon_path{ get; private set; }
    public int type { get; private set; }
    public int date_margin { get; private set; }


  }
}
