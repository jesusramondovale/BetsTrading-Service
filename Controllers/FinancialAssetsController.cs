using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Diagnostics.Metrics;


namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class FinancialAssetsController : ControllerBase
  {
    private readonly AppDbContext _context;
    private readonly ICustomLogger _logger;

    public FinancialAssetsController(AppDbContext context, ICustomLogger customLogger)
    {
      _context = context;
      _logger = customLogger;
    }

    // GET: api/FinancialAssets (all)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FinancialAsset>>> GetFinancialAssets()
    {
      _logger.Log.Information("[FINANCIAL] :: GetFinancialAssets");
      return await _context.FinancialAssets.ToListAsync();
    }

    // POST: api/FinancialAssets/ByGroup/
    [HttpPost("ByGroup")]
    public async Task<ActionResult<IEnumerable<FinancialAsset>>> GetFinancialAssetsByGroup([FromBody] idRequest group)
    {

      var financialAssets = await _context.FinancialAssets
        .Where(fa => fa.group == group.id).OrderByDescending(fa => fa.current)
        .ToListAsync();

      if (financialAssets == null)
      {
        _logger.Log.Information("[FINANCIAL] :: ByGroup :: Not found . Group: {grp}", group.id);
        return NotFound();
      }
      _logger.Log.Debug("[FINANCIAL] :: ByGroup :: Success. Assets group : {msg}", group.id);
      return financialAssets;
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
    public async Task<ActionResult<FinancialAsset>> FetchCandlesAsync([FromBody] symbolWithTimeframe symbol)
    {
      int timeframe = symbol.timeframe ?? 1;

      var financialAsset = await _context.FinancialAssets
          .FirstOrDefaultAsync(fa => fa.ticker == symbol.id);

      if (financialAsset == null) return NotFound();

      // Si timeframe = 1 devolvemos los datos tal cual
      if (timeframe <= 1) return financialAsset;

      // Normalizamos arrays a orden cronológico (antiguo → reciente)
      var closes = (financialAsset.close ?? Array.Empty<double>()).Reverse().ToArray();
      var opens = (financialAsset.open ?? Array.Empty<double>()).Reverse().ToArray();
      var highs = (financialAsset.daily_max ?? Array.Empty<double>()).Reverse().ToArray();
      var lows = (financialAsset.daily_min ?? Array.Empty<double>()).Reverse().ToArray();

      int groupedCount = closes.Length / timeframe;

      List<double> groupedClose = new();
      List<double> groupedOpen = new();
      List<double> groupedHigh = new();
      List<double> groupedLow = new();

      for (int i = 0; i < groupedCount; i++)
      {
        int start = i * timeframe;
        int end = Math.Min(start + timeframe, closes.Length);

        var blockCloses = closes.Skip(start).Take(end - start).ToArray();
        var blockOpens = opens.Skip(start).Take(end - start).ToArray();
        var blockHighs = highs.Skip(start).Take(end - start).ToArray();
        var blockLows = lows.Skip(start).Take(end - start).ToArray();

        if (blockCloses.Length == 0) continue;

        // open = apertura de la PRIMERA vela del bloque
        double openValue = blockOpens.First();

        // close = cierre de la ÚLTIMA vela del bloque
        double closeValue = blockCloses.Last();

        groupedOpen.Add(openValue);
        groupedClose.Add(closeValue);
        groupedHigh.Add(blockHighs.Max());
        groupedLow.Add(blockLows.Min());
      }

      // Invertimos de nuevo para mantener el formato esperado (última vela primero)
      groupedOpen.Reverse();
      groupedClose.Reverse();
      groupedHigh.Reverse();
      groupedLow.Reverse();

      var groupedAsset = new FinancialAsset(
          financialAsset.id,
          financialAsset.name,
          financialAsset.group,
          financialAsset.icon,
          financialAsset.country,
          financialAsset.ticker,
          financialAsset.current,
          groupedClose.ToArray()
      )
      {
        open = groupedOpen.ToArray(),
        daily_max = groupedHigh.ToArray(),
        daily_min = groupedLow.ToArray()
      };

      return groupedAsset;
    }

  }
}
