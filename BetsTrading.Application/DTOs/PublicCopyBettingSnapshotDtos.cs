using System.Text.Json.Serialization;

namespace BetsTrading.Application.DTOs;

/// <summary>Estadísticas de apuestas de zona finalizadas (histórico archivado) para perfil público Copy-Betting.</summary>
public sealed class CopyBettingStatsDto
{
    public int FinishedBets { get; set; }
    public double TotalStakedCoins { get; set; }
    public double TotalProfitLossCoins { get; set; }
    public int Wins { get; set; }
    public int Losses { get; set; }
    /// <summary>Porcentaje de aciertos sobre apuestas de zona finalizadas, o null si no hay datos.</summary>
    public double? WinRatePercent { get; set; }
}

/// <summary>Una fila reciente: apuesta de zona o apuesta a precio exacto.</summary>
public sealed class CopyBettingRecentItemDto
{
    public string Kind { get; set; } = "";

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public BetDto? Bet { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public PriceBetDto? PriceBet { get; set; }
}

/// <summary>Estado de copy-trading del usuario autenticado (follower) respecto al perfil consultado.</summary>
public sealed class ViewerCopyTradingStatusDto
{
    public bool IsActive { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? CopyPercent { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? AutoAdjustByBalance { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public bool? StopAfterOneLoss { get; set; }
}

public sealed class PublicCopyBettingSnapshotDto
{
    public CopyBettingStatsDto Stats { get; set; } = new();
    public List<CopyBettingRecentItemDto> RecentBets { get; set; } = new();

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public ViewerCopyTradingStatusDto? ViewerCopyTrading { get; set; }
}
