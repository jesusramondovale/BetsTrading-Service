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

    // GET: api/FinancialAssets/ByGroup/{group}
    [HttpGet("ByGroup/{group}")]
    public async Task<ActionResult<IEnumerable<FinancialAsset>>> GetFinancialAssetsByGroup(string group)
    {
      
      var financialAssets = await _context.FinancialAssets
        .Where(fa => fa.group == group)
        .ToListAsync();

      if (financialAssets == null)
      {
        _logger.Log.Information("[FINANCIAL] :: ByGroup :: Not found . Group: {grp}", group);
        return NotFound();
      }
      _logger.Log.Information("[FINANCIAL] :: ByGroup :: Success. Assets group : {msg}", group);
      return financialAssets;                 
    }

    // GET: api/FinancialAssets/ByCountry/{country}
    [HttpGet("ByCountry/{country}")]
    public async Task<ActionResult<IEnumerable<FinancialAsset>>> GetFinancialAssetsByCountry(string country)
    {    
      var financialAssets = await _context.FinancialAssets
        .Where(fa => fa.country == country)
        .ToListAsync();

      if (financialAssets == null)
      {
        _logger.Log.Information("[FINANCIAL] :: ByCountry :: Not found . Group: {grp}", country);
        return NotFound();
      }
      _logger.Log.Information("[FINANCIAL] :: ByCountry :: Success. Assets country : {msg}", country);
      return financialAssets;

    }

    // GET: api/FinancialAssets/FetchCandles
    [HttpPost("FetchCandles")]
    public async Task<ActionResult<FinancialAsset>> FetchCandlesAsync([FromBody] idRequest symbol)
    {
      var financialAsset = await _context.FinancialAssets.FirstOrDefaultAsync(fa => fa.ticker == symbol.id);
      return (financialAsset == null) ? NotFound() : financialAsset;
      
    }
  }
}
