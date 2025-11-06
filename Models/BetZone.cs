using System.Text.Json.Serialization;

namespace BetsTrading_Service.Models
{
  public class BetZone
  {
    public int id { get; set; } 
    public string ticker { get; set; } 
    public double target_value { get; set; } 
    public double bet_margin { get; set; }
    public DateTime start_date { get; set; }
    public DateTime end_date { get; set; }
    public double target_odds { get; set; }
    public int bet_type{ get; set; }
    public bool active { get; set; }
    public int timeframe { get; set; }



    [JsonConstructor]
    public BetZone(int id, string ticker, double target_value, double bet_margin, DateTime start_date, DateTime end_date, double target_odds, int bet_type, int timeframe)
    {
      this.id = id;
      this.ticker = ticker;
      this.target_value = target_value;
      this.bet_margin = bet_margin;
      this.start_date = start_date;
      this.end_date = end_date;
      this.target_odds = target_odds;
      this.bet_type = bet_type;
      this.active = true;
      this.timeframe = timeframe;
    }

    public BetZone(string ticker, double target_value, double bet_margin, DateTime start_date, DateTime end_date, double target_odds, int bet_type, int timeframe)
    {
      this.ticker = ticker;
      this.target_value = target_value;
      this.bet_margin = bet_margin;
      this.start_date = start_date;
      this.end_date = end_date;
      this.target_odds = target_odds;
      this.active = true;
      this.bet_type = bet_type;
      this.timeframe = timeframe;
    }

  }
}
