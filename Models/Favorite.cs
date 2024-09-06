namespace BetsTrading_Service.Models
{
  public class Favorite
  {

    public Favorite(string id, string user_id, string ticker )

    {
      this.id = id;
      this.user_id = user_id;
      this.ticker = ticker;

    }


    public string id { get; private set; }
    public string user_id { get; private set; }
    public string ticker { get; private set; }


  }

  public class FavoriteDTO
  {
    public FavoriteDTO(string id, string name, string icon, double daily_gain, double close, double current, string user_id, string ticker)

    {
      this.id = id;
      this.name = name;
      this.icon = icon;
      this.daily_gain = daily_gain;
      this.close = close;
      this.current = current;
      this.user_id = user_id;
      this.ticker = ticker;

    }


    public string id { get; private set; }
    public string name { get; private set; }
    public string icon { get; private set; }
    public double daily_gain { get; private set; }
    public double close { get; private set; }
    public double current { get; private set; }
    public string user_id { get; private set; }
    public string ticker { get; private set; }


  }



}

