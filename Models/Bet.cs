namespace BetsTrading_Service.Models
{
  public class Bet
  {

    // Empty constructor
    public Bet() {
      this.user_id = "";
      this.ticker = "";
    }

    // Full constructor
    public Bet(string user_id , string ticker,
                      double bet_amount, double origin_value, double origin_odds,
                      double target_value, double target_margin,
                      bool target_won, bool finished, bool paid, int bet_zone) 
    
    {
      this.user_id = user_id;
      this.ticker = ticker;
      this.bet_amount = bet_amount;
      this.origin_value = origin_value;
      this.origin_odds = origin_odds;
      this.target_won = target_won;
      this.finished = finished;
      this.paid = paid;
      this.bet_zone = bet_zone;
      this.archived = false;

    }
        

    public int id { get; set; }
    public string user_id { get;  set; }
    public string ticker { get;  set; }
    public double bet_amount{ get;  set; }
    public double origin_value { get;  set; }
    public double origin_odds { get; set; }
    public bool target_won { get; set; }
    public bool finished { get; set; }
    public bool paid { get; set; }
    public int bet_zone{ get; set; }
    public bool archived { get; set; }


  }

  public class BetDTO(int id, string user_id, string ticker, string name,
                    double bet_amount, double necessary_gain, double origin_value, double current_value,
                    double target_value, double target_margin, DateTime target_date, DateTime end_date,
                    double target_odds, bool target_won, bool finished, string icon_path, int type, int date_margin, int bet_zone)
  {
    public int id { get; set; } = id;
    public string user_id { get; set; } = user_id;
    public string name { get; set; } = name;
    public string ticker { get; set; } = ticker;
    public double bet_amount { get; set; } = bet_amount;
    public double necessary_gain { get; set; } = necessary_gain;
    public double origin_value { get; set; } = origin_value;
    public double current_value { get; set; } = current_value;
    public double target_value { get; set; } = target_value;
    public double target_margin { get; set; } = target_margin;
    public DateTime? target_date { get; set; } = target_date;
    public DateTime? final_date { get; set; } = end_date;
    public double target_odds { get; set; } = target_odds;
    public bool target_won { get; set; } = target_won;
    public bool finished { get; set; } = finished;
    public string icon_path { get; set; } = icon_path;
    public int type { get; set; } = type;
    public int date_margin { get; set; } = date_margin;
    public int bet_zone { get; set; } = bet_zone;
    public bool archived { get; set; } = false;

  }

  public class PriceBetDTO(int id, string name, string ticker, double price_bet,
              bool paid, double margin, string user_id,
              DateTime bet_date, DateTime end_date, string icon_path)
  {
    public int id { get; set; } = id;
    public string name { get; set; } = name;
    public string ticker { get; set; } = ticker;
    public double price_bet { get; set; } = price_bet;
    public bool paid { get; set; } = paid;
    public double margin { get; set; } = margin;
    public string user_id { get; set; } = user_id;
    public DateTime bet_date { get; set; } = bet_date;
    public DateTime end_date { get; set; } = end_date;
    public String icon_path { get; set; } = icon_path;
    public bool archived { get; set; } = false;

  }

}
