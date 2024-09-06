using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;

namespace BetsTrading_Service.Models
{
  public class Bet
  {

    // Empty constructor
    public Bet() { }

    // Full constructor
    public Bet(int id, string user_id , string ticker,
                      double bet_amount, double origin_value,
                      double target_value, double target_margin,
                      bool target_won, int bet_zone) 
    
    {
      this.id = id;
      this.user_id = user_id;
      this.ticker = ticker;
      this.bet_amount = bet_amount;
      this.origin_value = origin_value;
      this.target_won = target_won;
      this.bet_zone = bet_zone;
    }
        

    public int id { get;  set; }
    public string user_id { get;  set; }
    public string ticker { get;  set; }
    public double bet_amount{ get;  set; }
    public double origin_value { get;  set; }
    public bool target_won { get; set; }
    public int bet_zone{ get; set; }


  }

  public class BetDTO
  {
    public BetDTO(int id, string user_id, string ticker, string name,
                      double bet_amount, double origin_value, double current_value,
                      double target_value, double target_margin, DateTime target_date,
                      double target_odds, bool target_won, string icon_path, int type, int date_margin)

    {
      this.id = id;
      this.user_id = user_id;
      this.ticker = ticker;
      this.name = name;
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


    public int id { get; set; }
    public string user_id { get; set; }
    public string name { get; set; }
    public string ticker { get; set; }
    public double bet_amount { get; set; }
    public double origin_value { get; set; }
    public double current_value { get; set; }
    public double target_value { get; set; }
    public double target_margin { get; set; }
    public DateTime target_date { get; set; }
    public double target_odds { get; set; }
    public bool target_won { get; set; }
    public string icon_path { get; set; }
    public int type { get; set; }
    public int date_margin { get; set; }


  }

}
