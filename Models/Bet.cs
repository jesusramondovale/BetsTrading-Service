using Microsoft.EntityFrameworkCore.Storage.ValueConversion.Internal;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using System.ComponentModel.DataAnnotations.Schema;

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


  }

  public class BetDTO
  {
    public BetDTO(int id, string user_id, string ticker, string name,
                      double bet_amount, double daily_gain, double origin_value, double current_value,
                      double target_value, double target_margin, DateTime target_date, DateTime end_date,
                      double target_odds, bool target_won, string icon_path, int type, int date_margin, int bet_zone)

    {
      
      this.id = id;
      this.user_id = user_id;
      this.ticker = ticker;
      this.name = name;
      this.bet_amount = bet_amount;
      this.daily_gain = daily_gain;
      this.origin_value = origin_value;
      this.current_value = current_value;
      this.target_value = target_value;
      this.target_margin = target_margin;
      this.target_date = target_date;
      this.final_date = end_date;
      this.target_odds = target_odds;
      this.target_won = target_won;
      this.icon_path = icon_path;
      this.type = type;
      this.date_margin = date_margin;
      this.bet_zone = bet_zone;
    }


    public int id { get; set; }
    public string user_id { get; set; }
    public string name { get; set; }
    public string ticker { get; set; }
    public double bet_amount { get; set; }
    public double daily_gain { get; set; }
    public double origin_value { get; set; }
    public double current_value { get; set; }
    public double target_value { get; set; }
    public double target_margin { get; set; }
    public DateTime? target_date { get; set; }
    public DateTime? final_date { get; set; }
    public double target_odds { get; set; }
    public bool target_won { get; set; }
    public string icon_path { get; set; }
    public int type { get; set; }
    public int date_margin { get; set; }
    public int bet_zone { get; set; }

  }

  public class PriceBetDTO
  {
    public PriceBetDTO(int id, string name, string ticker, double price_bet, 
                bool paid, double margin, string user_id, 
                DateTime bet_date, DateTime end_date, string icon_path)
    {
      this.id = id;
      this.name = name;
      this.ticker = ticker;
      this.price_bet = price_bet;
      this.paid = paid;
      this.margin = margin;
      this.user_id = user_id;
      this.bet_date = bet_date;
      this.end_date = end_date;
      this.icon_path = icon_path;

    }

    public int id { get; set; }
    public string name { get; set; }
    public string ticker { get; set; }
    public double price_bet { get; set; }
    public bool paid{ get; set; }
    public double margin { get; set; }
    public string user_id { get; set; }
    public DateTime bet_date { get; set; }
    public DateTime end_date { get; set; }
    public String icon_path { get; set; }

  }

}
