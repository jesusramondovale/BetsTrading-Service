namespace BetsTrading_Service.Models
{
  public class Favorite(string id, string user_id, string ticker)
  {
    public string id { get; private set; } = id;
    public string user_id { get; private set; } = user_id;
    public string ticker { get; private set; } = ticker;


  }

  public class FavoriteDTO(string id, string name, string icon, double daily_gain, double close, double current, string user_id, string ticker)
  {
    public string id { get; private set; } = id;
    public string name { get; private set; } = name;
    public string icon { get; private set; } = icon;
    public double daily_gain { get; private set; } = daily_gain;
    public double close { get; private set; } = close;
    public double current { get; private set; } = current;
    public string user_id { get; private set; } = user_id;
    public string ticker { get; private set; } = ticker;


  }



}

