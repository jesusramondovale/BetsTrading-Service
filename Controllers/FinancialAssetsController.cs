using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class FinancialAssetsController(AppDbContext context, ICustomLogger customLogger) : ControllerBase
  {
    private readonly AppDbContext _context = context;
    private readonly ICustomLogger _logger = customLogger;
       
    // GET: api/FinancialAssets (all)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FinancialAsset>>> GetFinancialAssets()
    {
      _logger.Log.Information("[FINANCIAL] :: GetFinancialAssets");
      return await _context.FinancialAssets.ToListAsync();
    }

    // POST: api/FinancialAssets/ByGroup/
    [HttpPost("ByGroup")]
    public async Task<ActionResult<IEnumerable<FinancialAssetDTO>>> GetFinancialAssetsByGroup([FromBody] idRequestWithCurrency request, CancellationToken ct)
    {
      try
      {
        ActionResult<IEnumerable<FinancialAssetDTO>> result;
        if (request.currency == "EUR")
        {
          result = await InnerGetFinancialAssetsByGroup(request, ct);
        }
        else
        {
          result = await InnerGetFinancialAssetsByGroupUSD(request, ct);
        }
        return result;
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[FINANCIAL] :: ByGroup :: Server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [NonAction]
    public async Task<ActionResult<IEnumerable<FinancialAssetDTO>>> InnerGetFinancialAssetsByGroup(idRequestWithCurrency request, CancellationToken ct)
    {
      var financialAssets = await _context.FinancialAssets
        .AsNoTracking()
        .Where(fa => fa.group == request.id)
        .OrderByDescending(fa => fa.current_eur)
        .ToListAsync(ct);

      if (financialAssets == null || financialAssets.Count == 0)
      {
        _logger.Log.Information("[FINANCIAL] :: ByGroup :: Not found . Group: {grp}", request.id);
        return NotFound();
      }

      var assetIds = financialAssets.Select(a => a.id).ToList();

      // Cargar todas las velas necesarias en una sola consulta
      var allCandles = await _context.AssetCandles
        .AsNoTracking()
        .Where(c => assetIds.Contains(c.AssetId) && c.Interval == "1h")
        .OrderByDescending(c => c.DateTime)
        .ToListAsync(ct);

      // Agrupar velas por AssetId para procesamiento eficiente
      var candlesByAsset = allCandles
        .GroupBy(c => c.AssetId)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.DateTime).ToList());

      var assetsDTO = new List<FinancialAssetDTO>();

      foreach (var asset in financialAssets)
      {
        if (!candlesByAsset.TryGetValue(asset.id, out var assetCandles) || assetCandles.Count == 0)
        {
          // Si no hay velas, devolvemos el activo sin precios calculados
          assetsDTO.Add(new FinancialAssetDTO
          {
            id = asset.id,
            name = asset.name,
            group = asset.group,
            icon = asset.icon,
            country = asset.country,
            ticker = asset.ticker,
            current_eur = asset.current_eur,
            current_usd = asset.current_usd,
            current = null,
            close = null,
            daily_gain = null
          });
          continue;
        }

        var lastCandle = assetCandles[0];
        var lastDay = lastCandle.DateTime.Date;

        AssetCandle? prevCandle = null;

        if (asset.group == "Cryptos" || asset.group == "Forex")
        {
          // Para Cryptos/Forex: buscar vela de hace 24 horas (índice 24)
          if (assetCandles.Count > 24)
          {
            prevCandle = assetCandles[24];
          }
        }
        else
        {
          // Para otros activos: buscar la última vela del día anterior
          prevCandle = assetCandles.FirstOrDefault(c => c.DateTime.Date < lastDay);
        }

        double prevClose;
        double dailyGain;
        double currentClose = (double)asset.current_eur;
        double current = (double)lastCandle.Close;

        if (prevCandle != null)
        {
          prevClose = (double)prevCandle.Close;
          dailyGain = prevClose == 0 ? 0 : ((currentClose - prevClose) / prevClose) * 100.0;
        }
        else
        {
          prevClose = asset.current_eur * 0.95;
          dailyGain = ((currentClose - prevClose) / prevClose) * 100.0;
        }

        assetsDTO.Add(new FinancialAssetDTO
        {
          id = asset.id,
          name = asset.name,
          group = asset.group,
          icon = asset.icon,
          country = asset.country,
          ticker = asset.ticker,
          current_eur = asset.current_eur,
          current_usd = asset.current_usd,
          current = current,
          close = prevClose,
          daily_gain = dailyGain
        });
      }

      _logger.Log.Debug("[FINANCIAL] :: ByGroup :: Success. Assets group : {msg}", request.id);
      return Ok(assetsDTO);
    }

    [NonAction]
    public async Task<ActionResult<IEnumerable<FinancialAssetDTO>>> InnerGetFinancialAssetsByGroupUSD(idRequestWithCurrency request, CancellationToken ct)
    {
      var financialAssets = await _context.FinancialAssets
        .AsNoTracking()
        .Where(fa => fa.group == request.id)
        .OrderByDescending(fa => fa.current_usd)
        .ToListAsync(ct);

      if (financialAssets == null || financialAssets.Count == 0)
      {
        _logger.Log.Information("[FINANCIAL] :: ByGroup :: Not found . Group: {grp}", request.id);
        return NotFound();
      }

      var assetIds = financialAssets.Select(a => a.id).ToList();

      // Cargar todas las velas necesarias en una sola consulta
      var allCandles = await _context.AssetCandlesUSD
        .AsNoTracking()
        .Where(c => assetIds.Contains(c.AssetId) && c.Interval == "1h")
        .OrderByDescending(c => c.DateTime)
        .ToListAsync(ct);

      // Agrupar velas por AssetId para procesamiento eficiente
      var candlesByAsset = allCandles
        .GroupBy(c => c.AssetId)
        .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.DateTime).ToList());

      var assetsDTO = new List<FinancialAssetDTO>();

      foreach (var asset in financialAssets)
      {
        if (!candlesByAsset.TryGetValue(asset.id, out var assetCandles) || assetCandles.Count == 0)
        {
          // Si no hay velas, devolvemos el activo sin precios calculados
          assetsDTO.Add(new FinancialAssetDTO
          {
            id = asset.id,
            name = asset.name,
            group = asset.group,
            icon = asset.icon,
            country = asset.country,
            ticker = asset.ticker,
            current_eur = asset.current_eur,
            current_usd = asset.current_usd,
            current = null,
            close = null,
            daily_gain = null
          });
          continue;
        }

        var lastCandle = assetCandles[0];
        var lastDay = lastCandle.DateTime.Date;

        AssetCandleUSD? prevCandle = null;

        if (asset.group == "Cryptos" || asset.group == "Forex")
        {
          // Para Cryptos/Forex: buscar vela de hace 24 horas (índice 24)
          if (assetCandles.Count > 24)
          {
            prevCandle = assetCandles[24];
          }
        }
        else
        {
          // Para otros activos: buscar la última vela del día anterior
          prevCandle = assetCandles.FirstOrDefault(c => c.DateTime.Date < lastDay);
        }

        double prevClose;
        double dailyGain;
        double currentClose = (double)asset.current_usd;
        double current = (double)lastCandle.Close;

        if (prevCandle != null)
        {
          prevClose = (double)prevCandle.Close;
          dailyGain = prevClose == 0 ? 0 : ((currentClose - prevClose) / prevClose) * 100.0;
        }
        else
        {
          prevClose = asset.current_usd * 0.95;
          dailyGain = ((currentClose - prevClose) / prevClose) * 100.0;
        }

        assetsDTO.Add(new FinancialAssetDTO
        {
          id = asset.id,
          name = asset.name,
          group = asset.group,
          icon = asset.icon,
          country = asset.country,
          ticker = asset.ticker,
          current_eur = asset.current_eur,
          current_usd = asset.current_usd,
          current = current,
          close = prevClose,
          daily_gain = dailyGain
        });
      }

      _logger.Log.Debug("[FINANCIAL] :: ByGroup :: Success. Assets group : {msg}", request.id);
      return Ok(assetsDTO);
    }

    // POST: api/FinancialAssets/ByCountry/
    [HttpPost("ByCountry")]
    public async Task<ActionResult<IEnumerable<FinancialAsset>>> GetFinancialAssetsByCountry([FromBody] idRequest country)
    {
      var financialAssets = await _context.FinancialAssets
        .Where(fa => fa.country == country.id)
        .ToListAsync();

      if (financialAssets == null)
      {
        _logger.Log.Information("[FINANCIAL] :: ByCountry :: Not found . Group: {grp}", country);
        return NotFound();
      }
      _logger.Log.Debug("[FINANCIAL] :: ByCountry :: Success. Assets country : {msg}", country.id);
      return financialAssets;

    }

    // POST: api/FinancialAssets/FetchCandles
    [HttpPost("FetchCandles")]
    public async Task<ActionResult<IEnumerable<CandleDto>>> FetchCandlesAsync([FromBody] symbolWithTimeframe symbol, CancellationToken ct)
    {
      try
      {
        ActionResult<IEnumerable<CandleDto>> result; 
        if (symbol.currency == "EUR")
        {
          result = await InnerFetchCandles(symbol, ct);
        }
        else
        {
          result = await InnerFetchCandlesUSD(symbol, ct);
        }

        return result;

      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: FetchCandles :: Server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

    }

    [NonAction]
    public async Task<ActionResult<IEnumerable<CandleDto>>> InnerFetchCandles(symbolWithTimeframe symbol, CancellationToken ct)
    {
      int timeframe = symbol.timeframe ?? 1;
      var financialAsset = await _context.FinancialAssets
          .AsNoTracking()
          .FirstOrDefaultAsync(fa => fa.ticker == symbol.id, ct);

      if (financialAsset == null)
        return NotFound();

      List<AssetCandle> candles = await _context.AssetCandles
          .AsNoTracking()
          .Where(c => c.AssetId == financialAsset.id && c.Interval == "1h")
          .OrderByDescending(c => c.DateTime)
          .ToListAsync(ct);
      
      

      if (candles.Count == 0)
        return Ok(new List<CandleDto>());

      if (timeframe <= 1)
      {
        var resultRaw = candles.Select(c => new CandleDto
        {
          DateTime = c.DateTime,
          Open = c.Open,
          High = c.High,
          Low = c.Low,
          Close = c.Close

        });

        return Ok(resultRaw);
      }

      var grouped = candles
        .GroupBy(c => new DateTime(
            c.DateTime.Year,
            c.DateTime.Month,
            c.DateTime.Day,
            c.DateTime.Hour / timeframe * timeframe,
            0, 0, DateTimeKind.Utc))
        .Select(g => new CandleDto
        {
          DateTime = g.Key,
          Open = g.OrderBy(c => c.DateTime).First().Open,
          Close = g.OrderBy(c => c.DateTime).Last().Close,
          High = g.Max(c => c.High),
          Low = g.Min(c => c.Low)
        })
        .OrderByDescending(c => c.DateTime)
        .ToList();

      return Ok(grouped);

    }

    [NonAction]
    public async Task<ActionResult<IEnumerable<CandleDto>>> InnerFetchCandlesUSD(symbolWithTimeframe symbol, CancellationToken ct)
    {
      int timeframe = symbol.timeframe ?? 1;
      var financialAsset = await _context.FinancialAssets
          .AsNoTracking()
          .FirstOrDefaultAsync(fa => fa.ticker == symbol.id, ct);

      if (financialAsset == null)
        return NotFound();

      List<AssetCandleUSD> candles = await _context.AssetCandlesUSD
          .AsNoTracking()
          .Where(c => c.AssetId == financialAsset.id && c.Interval == "1h")
          .OrderByDescending(c => c.DateTime)
          .ToListAsync(ct);


      if (candles.Count == 0)
        return Ok(new List<CandleDto>());

      if (timeframe <= 1)
      {
        var resultRaw = candles.Select(c => new CandleDto
        {
          DateTime = c.DateTime,
          Open = c.Open,
          High = c.High,
          Low = c.Low,
          Close = c.Close

        });

        return Ok(resultRaw);
      }

      var grouped = candles
        .GroupBy(c => new DateTime(
            c.DateTime.Year,
            c.DateTime.Month,
            c.DateTime.Day,
            c.DateTime.Hour / timeframe * timeframe,
            0, 0, DateTimeKind.Utc))
        .Select(g => new CandleDto
        {
          DateTime = g.Key,
          Open = g.OrderBy(c => c.DateTime).First().Open,
          Close = g.OrderBy(c => c.DateTime).Last().Close,
          High = g.Max(c => c.High),
          Low = g.Min(c => c.Low)
        })
        .OrderByDescending(c => c.DateTime)
        .ToList();

      return Ok(grouped);

    }

  }

}

