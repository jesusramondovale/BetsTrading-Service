namespace BetsTrading_Service.Models
{
  public class FinancialAsset
  {
    public FinancialAsset(string name, string group, string? icon, string? country, string? ticker, double current)
    {
      this.name = name;
      this.group = group;
      this.icon = icon;
      this.country = country;
      this.ticker = ticker;
      this.current = current;
    }

    public FinancialAsset(int id, string name, string group, string? icon, string? country, string? ticker, double current)
    {
      this.id = id;
      this.name = name;
      this.group = group;
      this.icon = icon;
      this.country = country;
      this.ticker = ticker;
      this.current = current;
    }

    public int id { get; set; }
    public string name { get; set; }
    public string group { get; set; }
    public string? icon { get; set; }
    public string? country { get; set; }
    public string? ticker { get; set; }

    public double current { get; set; }

    public ICollection<AssetCandle> Candles { get; set; } = new List<AssetCandle>();

  }

}


