namespace BetsTrading_Service.Models
{
  public class FinancialAsset
  {
    public FinancialAsset(string name, string group, string? icon, string? country, string? ticker, double current)
    {
      this.Name = name;
      this.Group = group;
      this.Icon = icon;
      this.Country = country;
      this.Ticker = ticker;
      this.Current = current;
    }

    public FinancialAsset(int id, string name, string group, string? icon, string? country, string? ticker, double current)
    {
      this.Id = id;
      this.Name = name;
      this.Group = group;
      this.Icon = icon;
      this.Country = country;
      this.Ticker = ticker;
      this.Current = current;
    }

    public int Id { get; set; }
    public string Name { get; set; }
    public string Group { get; set; }
    public string? Icon { get; set; }
    public string? Country { get; set; }
    public string? Ticker { get; set; }

    public double Current { get; set; }

    public ICollection<AssetCandle> Candles { get; set; } = new List<AssetCandle>();

  }

}


