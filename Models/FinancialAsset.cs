namespace BetsTrading_Service.Models
{
  public class FinancialAsset
  {
    public FinancialAsset(string name, string group, string? icon, string? country, string? ticker, double current_eur, double current_usd)
    {
      this.name = name;
      this.group = group;
      this.icon = icon;
      this.country = country;
      this.ticker = ticker;
      this.current_eur = current_eur;
      this.current_usd = current_usd;
    }

    public FinancialAsset(int id, string name, string group, string? icon, string? country, string? ticker, double current_eur, double current_usd)
    {
      this.id = id;
      this.name = name;
      this.group = group;
      this.icon = icon;
      this.country = country;
      this.ticker = ticker;
      this.current_eur = current_eur;
      this.current_usd = current_usd;
    }

    public int id { get; set; }
    public string name { get; set; }
    public string group { get; set; }
    public string? icon { get; set; }
    public string? country { get; set; }
    public string? ticker { get; set; }

    public double current_eur { get; set; }
    public double current_usd { get; set; }

    public ICollection<AssetCandle> Candles { get; set; } = new List<AssetCandle>();

  }

  public class FinancialAssetDTO
  {
    public int id { get; set; }
    public string name { get; set; }
    public string group { get; set; }
    public string? icon { get; set; }
    public string? country { get; set; }
    public string? ticker { get; set; }
    public double current_eur { get; set; }
    public double current_usd { get; set; }
    public double? current { get; set; }
    public double? close { get; set; }
    public double? daily_gain { get; set; }
  }

}


