namespace BetsTrading_Service.Models
{
  public class FinancialAsset
  {
    public int id { get; set; }
    public required string name { get; set; }
    public required string group { get; set; }
    public string? icon { get; set; }
    public string? country { get; set; }
  }


}


