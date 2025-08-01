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
    private readonly  ICustomLogger _logger;

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
      _logger.Log.Information("[FINANCIAL] :: ByGroup :: Success. Assets group : {msg}", group.id);
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
      _logger.Log.Information("[FINANCIAL] :: ByCountry :: Success. Assets country : {msg}", country.id);
      return financialAssets;

    }

    // POST: api/FinancialAssets/FetchCandles
    [HttpPost("FetchCandles")]
    public async Task<ActionResult<FinancialAsset>> FetchCandlesAsync([FromBody] idRequest symbol)
    {
      var financialAsset = await _context.FinancialAssets.FirstOrDefaultAsync(fa => fa.ticker == symbol.id);
      return (financialAsset == null) ? NotFound() : financialAsset;
      
    }
  }
}
