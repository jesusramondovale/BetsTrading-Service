namespace BetsTrading_Service.Models
{
  public class TwelveDataParser
  {
    public CustomMeta? Meta { get; set; }
    public List<CustomValue>? Values { get; set; }

    public class CustomMeta
    {
      public string? Symbol { get; set; }
      public string? Interval { get; set; }
      public string? Currency { get; set; }
      public string? Exchange_Timezone { get; set; }
      public string? Exchange { get; set; }
      public string? Mic_Code { get; set; }
      public string? Type { get; set; }
    }

    public class CustomValue
    {
      public string? Datetime { get; set; }
      public string? Open { get; set; }
      public string? High { get; set; }
      public string? Low { get; set; }
      public string? Close { get; set; }
      public string? Volume { get; set; }
    }

    public sealed class TwelveDataResponse
    {
      public TwelveMeta? Meta { get; set; }
      public List<TwelveBar> Values { get; set; } = new();
      public string? Status { get; set; }  // "ok" o "error"
                                           // Si hay error, podrían venir campos "code" y "message"
      public object? Code { get; set; }
      public object? Message { get; set; }
    }

    public sealed class TwelveMeta
    {
      public string? Symbol { get; set; }          // p.ej., "BTC/EUR"
      public string? Interval { get; set; }        // "1day"
      public string? Currency_Base { get; set; }   // "Bitcoin"
      public string? Currency_Quote { get; set; }  // "Euro"
      public string? Exchange { get; set; }        // "Coinbase Pro"
      public string? Type { get; set; }            // "Digital Currency"
    }

    public sealed class TwelveBar
    {
      public string? Datetime { get; set; } // ""2025-09-21 08:00:00"
      public string? Open { get; set; }
      public string? High { get; set; }
      public string? Low { get; set; }
      public string? Close { get; set; }
    }


  }

}
