namespace BetsTrading_Service.Models
{
  public class Trend
  {

    public Trend(int id, string name, string icon, double daily_gain, double close, double current, double ticker)

    {
      this.id = id;
      this.name = name;
      this.icon = icon;
      this.daily_gain = daily_gain;
      this.close = close;
      this.current = current;
      this.ticker = ticker;

    }

   
    public int id { get; private set; }
    public string name { get; private set; }
    public string icon { get; private set; }
    public double daily_gain { get; private set; }
    public double close { get; private set; }
    public double current{ get; private set; }
    public double ticker{ get; private set; }


  }
}
