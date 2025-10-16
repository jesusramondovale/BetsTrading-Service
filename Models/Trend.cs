namespace BetsTrading_Service.Models
{
  public class Trend(int id, double daily_gain, string ticker)
  {
    public int id { get; set; } = id;
    public double daily_gain { get; private set; } = daily_gain;
    public string ticker { get; private set; } = ticker;

  }

  public class TrendDTO(int id, string name, string icon, double daily_gain, double close, double current, string ticker)
  {
    public int id { get; private set; } = id;
    public string name { get; private set; } = name;
    public string icon { get; private set; } = icon;
    public double daily_gain { get; private set; } = daily_gain;
    public double close { get; private set; } = close;
    public double current { get; private set; } = current;
    public string ticker { get; private set; } = ticker;


  }

}
