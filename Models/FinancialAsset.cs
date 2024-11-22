namespace BetsTrading_Service.Models
{
  public class FinancialAsset
  {
    public FinancialAsset(string name, string group, string? icon, string? country, string? ticker, double current, List<double> close, List<double> open, List<double> daily_max, List<double> daily_min)
    {
      this.name = name;
      this.group = group;
      this.icon = icon;
      this.country = country;
      this.ticker = ticker;
      this.current = current;
      this.close = close;
      this.open = open;
      this.daily_max = daily_max;
      this.daily_min= daily_min;
    }

    public FinancialAsset(string name, string group, string? icon, string? country, string? ticker, double current, List<double> close)
    {
      this.name = name;
      this.group = group;
      this.icon = icon;
      this.country = country;
      this.ticker = ticker;
      this.current = current;
      this.close = close;
    }

    public int id { get; set; }
    public string name { get; set; }
    public string group { get; set; }
    public string? icon { get; set; }
    public string? country { get; set; }
    public string? ticker { get; set; }
    public double current { get; set; }
    public List<double> close { get; set; } 
    public List<double>? open { get; set; }
    public List<double>? daily_max{ get; set; }
    public List<double>? daily_min { get; set; }

  }

}


