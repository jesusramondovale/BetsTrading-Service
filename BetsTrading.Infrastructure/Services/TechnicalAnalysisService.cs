using BetsTrading.Domain.Entities;

namespace BetsTrading.Infrastructure.Services;

/// <summary>
/// Servicio de análisis técnico para cálculo de indicadores financieros
/// </summary>
public static class TechnicalAnalysisService
{
    /// <summary>
    /// Calcula la volatilidad EWMA (Exponentially Weighted Moving Average)
    /// Más reactiva que la desviación estándar simple
    /// </summary>
    public static double CalculateEWMAVolatility(List<double> returns, double lambda = 0.94)
    {
        if (returns.Count < 2) return 0.0;

        double variance = 0.0;
        double weightSum = 0.0;

        for (int i = returns.Count - 1; i >= 0; i--)
        {
            double weight = Math.Pow(lambda, returns.Count - 1 - i);
            variance += weight * returns[i] * returns[i];
            weightSum += weight;
        }

        return Math.Sqrt(variance / weightSum);
    }

    /// <summary>
    /// Calcula los retornos logarítmicos
    /// </summary>
    public static List<double> CalculateLogReturns(List<double> prices)
    {
        var returns = new List<double>();
        for (int i = 1; i < prices.Count; i++)
        {
            if (prices[i - 1] > 0)
                returns.Add(Math.Log(prices[i] / prices[i - 1]));
        }
        return returns;
    }

    /// <summary>
    /// Calcula el RSI (Relative Strength Index)
    /// </summary>
    public static double CalculateRSI(List<double> closes, int period = 14)
    {
        if (closes.Count < period + 1) return 50.0; // Neutral si no hay suficientes datos

        var gains = new List<double>();
        var losses = new List<double>();

        for (int i = closes.Count - period; i < closes.Count; i++)
        {
            double change = closes[i] - closes[i - 1];
            if (change > 0) gains.Add(change);
            else if (change < 0) losses.Add(Math.Abs(change));
        }

        double avgGain = gains.Count > 0 ? gains.Average() : 0.0;
        double avgLoss = losses.Count > 0 ? losses.Average() : 0.0;

        if (avgLoss == 0) return 100.0;
        double rs = avgGain / avgLoss;
        return 100.0 - (100.0 / (1.0 + rs));
    }

    /// <summary>
    /// Calcula las Bandas de Bollinger
    /// </summary>
    public static (double Upper, double Middle, double Lower) CalculateBollingerBands(
        List<double> closes, int period = 20, double numStdDev = 2.0)
    {
        if (closes.Count < period) period = closes.Count;

        var recentCloses = closes.Skip(Math.Max(0, closes.Count - period)).Take(period).ToList();
        double sma = recentCloses.Average();
        double variance = recentCloses.Average(c => Math.Pow(c - sma, 2));
        double stdDev = Math.Sqrt(variance);

        return (sma + numStdDev * stdDev, sma, sma - numStdDev * stdDev);
    }

    /// <summary>
    /// Detecta soportes y resistencias usando pivots locales
    /// </summary>
    public static (List<double> Supports, List<double> Resistances) DetectSupportResistance(
        List<double> highs, List<double> lows, List<double> closes, int lookback = 5)
    {
        var supports = new List<double>();
        var resistances = new List<double>();

        for (int i = lookback; i < closes.Count - lookback; i++)
        {
            // Detectar mínimos locales (soportes)
            bool isLocalMin = true;
            for (int j = i - lookback; j <= i + lookback; j++)
            {
                if (j != i && lows[j] < lows[i])
                {
                    isLocalMin = false;
                    break;
                }
            }
            if (isLocalMin && !supports.Contains(lows[i]))
                supports.Add(lows[i]);

            // Detectar máximos locales (resistencias)
            bool isLocalMax = true;
            for (int j = i - lookback; j <= i + lookback; j++)
            {
                if (j != i && highs[j] > highs[i])
                {
                    isLocalMax = false;
                    break;
                }
            }
            if (isLocalMax && !resistances.Contains(highs[i]))
                resistances.Add(highs[i]);
        }

        return (supports.OrderByDescending(s => s).Take(5).ToList(),
                resistances.OrderBy(r => r).Take(5).ToList());
    }

    /// <summary>
    /// Calcula la probabilidad de que el precio alcance un nivel usando movimiento browniano geométrico
    /// </summary>
    public static double CalculateReachProbability(
        double currentPrice, double targetPrice, double volatility,
        double timeToExpiryHours, double drift = 0.0)
    {
        if (timeToExpiryHours <= 0) return currentPrice == targetPrice ? 1.0 : 0.0;
        if (currentPrice <= 0 || targetPrice <= 0) return 0.01;

        // Convertir tiempo a años para el modelo
        double timeToExpiry = timeToExpiryHours / (365.0 * 24.0);

        // Anualizar volatilidad (asumiendo que viene en términos horarios)
        double annualizedVol = volatility * Math.Sqrt(24.0 * 365.0);

        // Usar movimiento browniano geométrico: dS = μS dt + σS dW
        double logPriceRatio = Math.Log(targetPrice / currentPrice);
        double adjustedDrift = drift - 0.5 * annualizedVol * annualizedVol; // Ajuste de Itô

        // Probabilidad usando distribución normal
        double mean = adjustedDrift * timeToExpiry;
        double variance = annualizedVol * annualizedVol * timeToExpiry;
        double stdDev = Math.Sqrt(variance);

        if (stdDev == 0 || double.IsNaN(stdDev)) return Math.Abs(logPriceRatio) < 0.001 ? 0.5 : 0.01;

        // Calcular probabilidad usando función de distribución acumulada normal
        double z = (logPriceRatio - mean) / stdDev;

        // Aproximación de la CDF normal usando función de error
        double probability = 0.5 * (1.0 + Erf(z / Math.Sqrt(2.0)));

        // Asegurar que la probabilidad esté en un rango razonable
        return Math.Max(0.01, Math.Min(0.99, probability));
    }

    /// <summary>
    /// Función de error complementaria para aproximar la CDF normal
    /// </summary>
    private static double Erf(double x)
    {
        // Aproximación de Abramowitz y Stegun
        double a1 = 0.254829592;
        double a2 = -0.284496736;
        double a3 = 1.421413741;
        double a4 = -1.453152027;
        double a5 = 1.061405429;
        double p = 0.3275911;

        int sign = x < 0 ? -1 : 1;
        x = Math.Abs(x);

        double t = 1.0 / (1.0 + p * x);
        double y = 1.0 - (((((a5 * t + a4) * t) + a3) * t + a2) * t + a1) * t * Math.Exp(-x * x);

        return sign * y;
    }

    /// <summary>
    /// Calcula el drift (tendencia) basado en regresión lineal de retornos
    /// </summary>
    public static double CalculateDrift(List<double> returns, double timeStep = 1.0)
    {
        if (returns.Count < 2) return 0.0;
        return returns.Average() / timeStep;
    }

    /// <summary>
    /// Convierte probabilidad a odds decimales
    /// </summary>
    public static double ProbabilityToOdds(double probability, double margin = 0.95)
    {
        if (probability <= 0) return 100.0; // Odds muy altas para probabilidad 0
        if (probability >= 1) return 1.01; // Odds mínimas para probabilidad 1

        double fairOdds = 1.0 / probability;
        return Math.Max(1.01, Math.Round(fairOdds * margin, 2));
    }

    /// <summary>
    /// Calcula la variación máxima porcentual en las últimas 10xT velas del timeframe
    /// Analiza el rango high-low de cada vela y la variación total del período
    /// </summary>
    public static double CalculateMaxVariationForTimeframe(List<AssetCandle> candles, double currentPrice)
    {
        if (candles == null || candles.Count == 0 || currentPrice <= 0)
            return 0.0;

        try
        {
            // Calcular variaciones individuales de cada vela (high-low range)
            var candleVariations = new List<double>();
            foreach (var candle in candles)
            {
                if (candle.Close > 0)
                {
                    // Variación porcentual de la vela (high-low range)
                    double candleRange = (double)(candle.High - candle.Low) / (double)candle.Close;
                    candleVariations.Add(candleRange);
                }
            }

            if (candleVariations.Count == 0)
                return 0.0;

            // Variación máxima de una sola vela
            double maxSingleCandleVariation = candleVariations.Max();

            // Variación total del período (precio máximo - precio mínimo)
            double maxPrice = candles.Max(c => (double)c.High);
            double minPrice = candles.Min(c => (double)c.Low);
            double totalRangePercent = (maxPrice - minPrice) / currentPrice;

            // Variación promedio ponderada (más peso a variaciones recientes)
            double weightedAvgVariation = 0.0;
            double weightSum = 0.0;
            for (int i = 0; i < candleVariations.Count; i++)
            {
                // Peso exponencial: más reciente = más peso
                double weight = Math.Pow(1.1, candleVariations.Count - i);
                weightedAvgVariation += candleVariations[i] * weight;
                weightSum += weight;
            }
            weightedAvgVariation = weightSum > 0 ? weightedAvgVariation / weightSum : 0.0;

            // Calcular desviación estándar de variaciones para detectar volatilidad extrema
            double avgVariation = candleVariations.Average();
            double variance = candleVariations.Average(v => Math.Pow(v - avgVariation, 2));
            double stdDev = Math.Sqrt(variance);

            // Usar el máximo entre:
            // 1. Variación máxima de una vela individual
            // 2. Variación total del período (ajustada)
            // 3. Promedio ponderado + 1 desviación estándar (para capturar volatilidad)
            double variation1 = maxSingleCandleVariation * 100.0; // Convertir a porcentaje
            double variation2 = totalRangePercent * 100.0;
            double variation3 = (weightedAvgVariation + stdDev) * 100.0;

            // Retornar el máximo, pero con un límite razonable (no más del 20%)
            double maxVariation = Math.Max(Math.Max(variation1, variation2), variation3);
            return Math.Min(maxVariation, 20.0); // Límite máximo del 20%
        }
        catch
        {
            return 0.0;
        }
    }
}
