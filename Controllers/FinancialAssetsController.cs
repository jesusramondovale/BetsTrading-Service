using BetsTrading_Service.Database;
using BetsTrading_Service.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BetsTrading_Service.Controllers
{
  [Route("api/[controller]")]
  [ApiController]
  public class FinancialAssetsController : ControllerBase
  {
    private readonly AppDbContext _context;

    public FinancialAssetsController(AppDbContext context)
    {
      _context = context;
    }

    // GET: api/FinancialAssets (all)
    [HttpGet]
    public async Task<ActionResult<IEnumerable<FinancialAsset>>> GetFinancialAssets()
    {
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
        return NotFound();
      }

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
        return NotFound();
      }
      return financialAssets;
    }


  }
}
