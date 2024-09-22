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
  public class BetController : ControllerBase 
  {
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;

    public BetController(AppDbContext dbContext, ICustomLogger customLogger)
    {
      _dbContext = dbContext;
      _logger = customLogger;
    }

    [HttpPost("UserBets")]
    public async Task<IActionResult> UserBets([FromBody] idRequest userInfoRequest)
    {
      
      try
      {
        var bets = await _dbContext.Bet.Where(bet => bet.user_id == userInfoRequest.id).ToListAsync();

        if (!bets.Any())
        {
            
          _logger.Log.Warning("[INFO] :: UserBets :: Empty list of bets on userID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User has no bets!" });
        }

        _logger.Log.Information("[INFO] :: UserBets :: success with ID: {msg}", userInfoRequest.id);
        var betDTOs = new List<BetDTO>();

        foreach (var bet in bets)
        {
          var tmpAsset = await _dbContext.FinancialAssets.FirstOrDefaultAsync(a => a.ticker == bet.ticker);
          if (null != tmpAsset)
          {
            double tmpAssetDailyGain = (tmpAsset.current - tmpAsset.close)/ tmpAsset.close;
            var tmpBetZone = await _dbContext.BetZones.FirstOrDefaultAsync(bz => bz.id == bet.bet_zone);
            TimeSpan timeMargin = (TimeSpan)(tmpBetZone!.end_date - tmpBetZone!.start_date)!;

            betDTOs.Add(new BetDTO(id: bet.id, user_id: userInfoRequest.id!, ticker: bet.ticker, name: tmpAsset!.name!,
              bet_amount: bet.bet_amount, daily_gain: tmpAssetDailyGain, origin_value: bet.origin_value, current_value: tmpAsset.current,
              target_value: tmpBetZone.target_value, target_margin: tmpBetZone.bet_margin, target_date: tmpBetZone.start_date,
              target_odds: tmpBetZone.target_odds, target_won: bet.target_won, icon_path: tmpAsset.icon!,
              type: tmpBetZone.type, date_margin: timeMargin.Days));

          }
        }

        return Ok(new { Message = "UserBets SUCCESS", Bets = betDTOs });
      }
      catch (Exception ex)
      {
          
        _logger.Log.Error("[INFO] :: UserBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
      
    }

    [HttpPost("NewBet")]
    public async Task<IActionResult> NewBet([FromBody] newBetRequest betRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var betZone = await _dbContext.BetZones.FirstOrDefaultAsync(bz => bz.id == betRequest.bet_zone);
          var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.id == betRequest.user_id);

          if (betZone == null) 
            throw new Exception("Unexistent bet zone");
          if (user == null)
            throw new Exception("Unexistent user");
          if (betRequest.bet_amount > user.points)
            throw new Exception("Not enough points");

          var newBet = new Bet(user_id: betRequest.user_id!, ticker: betRequest.ticker!, bet_amount: betRequest.bet_amount, 
                               origin_value: betRequest.origin_value, target_value: betZone!.target_value, 
                               target_margin: betZone.bet_margin, target_won: false, bet_zone: betRequest.bet_zone);

          if (newBet != null)
          {
            _dbContext.Bet.Add(newBet);
            user.points = user.points - Math.Abs(betRequest.bet_amount);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.Log.Information("[INFO] :: NewBet :: Bet created successfully for user: {msg}", betRequest.user_id);
            return Ok(new { });
          }

          throw new Exception("Unknown error");
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[INFO] :: NewBet :: Internal server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }

    [HttpPost("GetBetZones")]
    public async Task<IActionResult> GetBetZones([FromBody] idRequest ticker)
    {
      
      try
      {
        var betZones = await _dbContext.BetZones.Where(bz => bz.ticker == ticker.id).ToListAsync();

        if (betZones.Any())
        {
          _logger.Log.Information("[INFO] :: GetBets :: Success on ticker: {msg}", ticker.id);
          return Ok(new { bets = betZones });
        }
        else
        {
          _logger.Log.Warning("[INFO] :: GetBets :: Bets not found for ticker: {msg}", ticker.id);
          return NotFound(new { Message = "No bets found for this ticker" });
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: GetBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
      
    }

    [HttpPost("DeleteRecentBet")]
    public async Task<IActionResult> DeleteRecentBet([FromBody] idRequest betIdRequest)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var bet = await _dbContext.Bet.FirstOrDefaultAsync(u => u.id.ToString() == betIdRequest.id);

          if (bet != null)
          {
            _dbContext.Bet.Remove(bet);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.Log.Information("[INFO] :: DeleteRecentBet :: Bet removed successfully with ID: {msg}", betIdRequest.id);
            return Ok(new { });
          }
          else
          {
            await transaction.RollbackAsync();
            _logger.Log.Warning("[INFO] :: DeleteRecentBet :: Bet not found for ID: {msg}", betIdRequest.id);
            return NotFound(new { Message = "Bet not found" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[INFO] :: DeleteRecentBet :: Server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }

    [HttpPost("DeleteHistoricBet")]
    public async Task<IActionResult> DeleteHistoricBet([FromBody] idRequest userInfoRequestId)
    {
      using (var transaction = await _dbContext.Database.BeginTransactionAsync())
      {
        try
        {
          var bets = await _dbContext.Bet
              .Where(b => b.user_id == userInfoRequestId.id && _dbContext.BetZones.Any(bz => bz.id == b.bet_zone && bz.end_date < DateTime.UtcNow))
              .ToListAsync();

          if (bets != null && bets.Count > 0)
          {
            _dbContext.Bet.RemoveRange(bets);
            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            _logger.Log.Information("[INFO] :: DeleteHistoricBet :: Bets removed successfully for user: {msg}", userInfoRequestId.id);
            return Ok(new { Message = "Bets deleted successfully" });
          }
          else
          {
            await transaction.RollbackAsync();
            _logger.Log.Warning("[INFO] :: DeleteHistoricBet :: Bets not found for user: {msg}", userInfoRequestId.id);
            return NotFound(new { Message = "Bets not found for the user" });
          }
        }
        catch (Exception ex)
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[INFO] :: DeleteHistoricBet :: Server error: {msg}", ex.Message);
          return StatusCode(500, new { Message = "Server error", Error = ex.Message });
        }
      }
    }
  }
}
