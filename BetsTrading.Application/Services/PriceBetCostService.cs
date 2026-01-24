namespace BetsTrading.Application.Services;

public static class PriceBetCostService
{
    // Constantes para costos de apuestas según margen
    private const int PRICE_BET_COST_0_MARGIN = 50;
    private const int PRICE_BET_COST_1_MARGIN = 200;
    private const int PRICE_BET_COST_5_MARGIN = 1000;
    private const int PRICE_BET_COST_7_MARGIN = 1500;
    private const int PRICE_BET_COST_10_MARGIN = 5000;
    private const int PRICE_BET_PRIZE = 100000;
    private const int PRICE_BET_DAYS_MARGIN = 2;

    public static int GetBetCostFromMargin(double margin)
    {
        return margin switch
        {
            0.0 => PRICE_BET_COST_0_MARGIN,
            0.01 => PRICE_BET_COST_1_MARGIN,
            0.05 => PRICE_BET_COST_5_MARGIN,
            0.075 => PRICE_BET_COST_7_MARGIN,
            0.1 => PRICE_BET_COST_10_MARGIN,
            _ => PRICE_BET_COST_0_MARGIN,
        };
    }

    public static int GetPrize() => PRICE_BET_PRIZE;
    public static int GetDaysMargin() => PRICE_BET_DAYS_MARGIN;
}
