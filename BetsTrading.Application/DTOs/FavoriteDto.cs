namespace BetsTrading.Application.DTOs;

public class FavoriteDto
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = string.Empty;
    public double DailyGain { get; set; }
    public double Close { get; set; }
    public double Current { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Ticker { get; set; } = string.Empty;
    public double? CurrentMaxOdd { get; set; }
    public int? CurrentMaxOddDirection { get; set; }
    /// <summary>Id de la zona con el odd máximo; para abrir la confirmación de apuesta directa.</summary>
    public int? CurrentMaxOddZoneId { get; set; }
    /// <summary>Timeframe en horas (1, 2, 4, 24) del odd máximo.</summary>
    public int? CurrentMaxOddTimeframe { get; set; }
}
