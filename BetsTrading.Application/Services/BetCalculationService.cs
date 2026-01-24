using BetsTrading.Domain.Entities;

namespace BetsTrading.Application.Services;

public class BetCalculationService
{
    public static double CalculateNecessaryGain(FinancialAsset asset, BetZone betZone, string currency = "EUR")
    {
        double currentPrice = currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd;
        
        // Calcular límites
        double topLimit = betZone.TargetValue + (betZone.TargetValue * betZone.BetMargin / 200.0);
        double bottomLimit = betZone.TargetValue - (betZone.TargetValue * betZone.BetMargin / 200.0);

        // Calcular ganancia necesaria
        if (currentPrice < bottomLimit)
        {
            return ((bottomLimit - currentPrice) / currentPrice) * 100;
        }
        else if (currentPrice > topLimit)
        {
            return ((topLimit - currentPrice) / currentPrice) * 100;
        }
        else
        {
            // Dentro del rango objetivo
            return 0.0;
        }
    }

    public static double CalculateNecessaryGain(FinancialAsset asset, BetZoneUSD betZone, string currency = "USD")
    {
        double currentPrice = currency == "EUR" ? asset.CurrentEur : asset.CurrentUsd;
        
        // Calcular límites
        double topLimit = betZone.TargetValue + (betZone.TargetValue * betZone.BetMargin / 200.0);
        double bottomLimit = betZone.TargetValue - (betZone.TargetValue * betZone.BetMargin / 200.0);

        // Calcular ganancia necesaria
        if (currentPrice < bottomLimit)
        {
            return ((bottomLimit - currentPrice) / currentPrice) * 100;
        }
        else if (currentPrice > topLimit)
        {
            return ((topLimit - currentPrice) / currentPrice) * 100;
        }
        else
        {
            // Dentro del rango objetivo
            return 0.0;
        }
    }
}
