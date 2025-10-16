using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;


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
    public async Task<ActionResult<IEnumerable<CandleDto>>> FetchCandlesAsync([FromBody] symbolWithTimeframe symbol, CancellationToken ct)
    {
      int timeframe = symbol.timeframe ?? 1;

      var financialAsset = await _context.FinancialAssets
          .AsNoTracking()
          .FirstOrDefaultAsync(fa => fa.ticker == symbol.id, ct);

      if (financialAsset == null)
        return NotFound();

      var candles = await _context.AssetCandles
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
