namespace BetsTrading_Service.Models
{
  public class FinancialAsset
  {
    public FinancialAsset(int id, string name, string group, string? icon, string? country, string? ticker, double current, double close)
    {
      this.id = id;
      this.name = name;
      this.group = group;
      this.icon = icon;
      this.country = country;
      this.ticker = ticker;
      this.current = current;
      this.close = close;
    }


    public int id { get; set; }
    public required string name { get; set; }
    public required string group { get; set; }
    public string? icon { get; set; }
    public string? country { get; set; }
    public string? ticker { get; set; }
    public double current { get; set; }
    public double close{ get; set; }

  }


}


