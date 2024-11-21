using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Mvc;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class StoreController : ControllerBase
  {
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;

    public StoreController(AppDbContext dbContext, ICustomLogger customLogger)
    {
      _dbContext = dbContext;
      _logger = customLogger;

    }

    [HttpPost("AddCoins")]
    public async Task<IActionResult> AddCoins([FromBody] addCoinsRequest coinsRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var user = _dbContext.Users
              .FirstOrDefault(u => u.id == coinsRequest.user_id);

          if (user != null) // User exists
          {
            user.points += coinsRequest.reward ?? 0;
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();

            _logger.Log.Information("[INFO] :: AddCoins :: Success on user ID: {msg}", coinsRequest.user_id);
            return Ok(new { });

          }
          else // Unexistent user
          {
            _logger.Log.Warning("[WARN] :: AddCoins :: User not found for ID: {msg}", coinsRequest.user_id);
            return NotFound(new { Message = "User not found" }); // User not found
          }
        }
        catch (Exception ex)
        {
          _logger.Log.Error("[ERROR] :: AddCoins :: Internal server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }

      }

    }

  }
}
