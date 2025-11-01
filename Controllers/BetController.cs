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
  public class BetController(AppDbContext dbContext, ICustomLogger customLogger) : ControllerBase 
  {
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ICustomLogger _logger = customLogger;
    //TODO
    private readonly int PRICE_BET_COST_0_MARGIN = 200;
    private readonly int PRICE_BET_COST_1_MARGIN = 350;
    private readonly int PRICE_BET_COST_5_MARGIN = 500;
    private readonly int PRICE_BET_COST_7_MARGIN = 800;
    private readonly int PRICE_BET_COST_10_MARGIN = 1500;
    private readonly int PRICE_BET_DAYS_MARGIN = 2;

    [HttpPost("UserBets")]
    public async Task<IActionResult> UserBets([FromBody] idRequest userInfoRequest, CancellationToken ct)
    {
      try
      {
        bool userExists = await _dbContext.Users.AnyAsync(u => u.id == userInfoRequest.id, ct);
        if (!userExists)
        {
          _logger.Log.Warning("[INFO] :: UserBets :: User doesn't exist. ID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "Unexistent user" });
        }

        var bets = await _dbContext.Bet
            .AsNoTracking()
            .Where(bet => bet.user_id == userInfoRequest.id)
            .ToListAsync(ct);

        if (bets.Count == 0)
        {
          _logger.Log.Debug("[INFO] :: UserBets :: Empty list of bets on userID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User has no bets!" });
        }

        var betDTOs = new List<BetDTO>();

        foreach (var bet in bets)
        {
          var tmpAsset = await _dbContext.FinancialAssets
              .AsNoTracking()
              .FirstOrDefaultAsync(a => a.ticker == bet.ticker, ct);

          if (tmpAsset == null) continue;


          var lastCandle = await _dbContext.AssetCandles
              .AsNoTracking()
              .Where(c => c.AssetId == tmpAsset.id && c.Interval == "1h")
              .OrderByDescending(c => c.DateTime)
              .FirstOrDefaultAsync(ct);

          if (lastCandle == null) continue;

          var lastDay = lastCandle.DateTime.Date;


          AssetCandle? prevCandle;

          if (tmpAsset.group == "Cryptos" || tmpAsset.group == "Forex")
          {
            prevCandle = await _dbContext.AssetCandles
                .AsNoTracking()
                .Where(c => c.AssetId == tmpAsset.id && c.Interval == "1h")
                .OrderByDescending(c => c.DateTime)
                .Skip(24)
                .FirstOrDefaultAsync(ct);
          }
          else
          {
            prevCandle = await _dbContext.AssetCandles
                .AsNoTracking()
                .Where(c => c.AssetId == tmpAsset.id && c.Interval == "1h" && c.DateTime.Date < lastDay)
                .OrderByDescending(c => c.DateTime)
                .FirstOrDefaultAsync(ct);
          }


         
          double necessaryGain;

          var tmpBetZone = await _dbContext.BetZones
              .AsNoTracking()
              .FirstOrDefaultAsync(bz => bz.id == bet.bet_zone, ct);

          necessaryGain = (tmpBetZone != null) ? ((tmpBetZone.target_value-tmpAsset.current) / tmpAsset.current) : 0;

          if (tmpBetZone == null) continue;

          double topLimit = tmpBetZone.target_value + (tmpBetZone.target_value * tmpBetZone.bet_margin/200.0);
          double bottomLimit = tmpBetZone.target_value - (tmpBetZone.target_value * tmpBetZone.bet_margin/200.0);

          if (tmpAsset.current < bottomLimit) {
            necessaryGain = (tmpBetZone != null) ? ((bottomLimit - tmpAsset.current) / tmpAsset.current) : 0;
          }
          if (tmpAsset.current > topLimit)
          {
            necessaryGain = (tmpBetZone != null) ? ((topLimit - tmpAsset.current) / tmpAsset.current) : 0;
          }
          else if (tmpAsset.current >= bottomLimit && tmpAsset.current <= topLimit) {
            necessaryGain = 0.0;
          }



          TimeSpan timeMargin = tmpBetZone!.end_date - tmpBetZone.start_date;

          betDTOs.Add(new BetDTO(
              id: bet.id,
              user_id: userInfoRequest.id!,
              ticker: bet.ticker,
              name: tmpAsset.name,
              bet_amount: bet.bet_amount,
              necessary_gain: necessaryGain*100,
              origin_value: bet.origin_value,
              current_value: tmpAsset.current,
              target_value: tmpBetZone.target_value,
              target_margin: tmpBetZone.bet_margin,
              target_date: tmpBetZone.start_date,
              end_date: tmpBetZone.end_date,
              target_odds: bet.origin_odds,
              target_won: bet.target_won,
              finished: bet.finished,
              icon_path: tmpAsset.icon ?? "noIcon",
              type: tmpBetZone.type,
              date_margin: timeMargin.Days,
              bet_zone: bet.bet_zone
          ));
        }

        _logger.Log.Debug("[INFO] :: UserBets :: success with ID: {msg}", userInfoRequest.id);

        return Ok(new { Message = "UserBets SUCCESS", Bets = betDTOs });
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[INFO] :: UserBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("UserPriceBets")]
    public async Task<IActionResult> UserPriceBets([FromBody] idRequest userInfoRequest, CancellationToken ct)
    {
      try
      {
        bool userExists = await _dbContext.Users.AnyAsync(u => u.id == userInfoRequest.id, ct);
        if (!userExists)
        {
          _logger.Log.Warning("[INFO] :: UserPriceBets :: User doesn't exist. ID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "Unexistent user" });
        }

        var priceBets = await _dbContext.PriceBets
            .AsNoTracking()
            .Where(bet => bet.user_id == userInfoRequest.id)
            .ToListAsync(ct);

        if (priceBets.Count == 0)
        {
          _logger.Log.Debug("[INFO] :: UserPriceBets :: Empty list of price bets on userID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User has no bets!" });
        }

        var priceBetDTOs = new List<PriceBetDTO>();

        foreach (var priceBet in priceBets)
        {
          var tmpAsset = await _dbContext.FinancialAssets
              .AsNoTracking()
              .FirstOrDefaultAsync(asset => asset.ticker == priceBet.ticker, ct);

          if (tmpAsset == null) continue;

          priceBetDTOs.Add(new PriceBetDTO(
              id: priceBet.id,
              name: tmpAsset.name,
              ticker: priceBet.ticker,
              price_bet: priceBet.price_bet,
              paid: priceBet.paid,
              margin: priceBet.margin,
              user_id: priceBet.user_id,
              bet_date: priceBet.bet_date,
              end_date: priceBet.end_date,
              icon_path: tmpAsset.icon ?? "noIcon"
          ));
        }

        _logger.Log.Debug("[INFO] :: UserPriceBets :: success with ID: {msg}", userInfoRequest.id);

        return Ok(new { Message = "UserPriceBets SUCCESS", Bets = priceBetDTOs });
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[INFO] :: UserPriceBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("NewBet")]
    public async Task<IActionResult> NewBet([FromBody] newBetRequest betRequest)
    {
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var betZone = await _dbContext.BetZones.FirstOrDefaultAsync(bz => bz.id == betRequest.bet_zone);
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.fcm == betRequest.fcm && u.id == betRequest.user_id);

        if (betZone == null)
          throw new Exception("Unexistent bet zone");
        if (user == null)
          throw new Exception("Unexistent user");
        if (betRequest.bet_amount > user.points)
          throw new Exception("Not enough points");

        var newBet = new Bet(user_id: betRequest.user_id!, ticker: betRequest.ticker!, bet_amount: betRequest.bet_amount,
                             origin_value: betRequest.origin_value, origin_odds: betZone.target_odds, target_value: betZone!.target_value,
                             target_margin: betZone.bet_margin, target_won: false, finished: false, paid: false, bet_zone: betRequest.bet_zone);

        if (newBet != null)
        {
          _dbContext.Bet.Add(newBet);
          user.points -= Math.Abs(betRequest.bet_amount);
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

    [HttpPost("CurrentPriceBets")]
    public async Task<IActionResult> PriceBets([FromBody] idRequest userInfoRequest)
    {

      try
      {
        var priceBets = await _dbContext.PriceBets.Where(pb => pb.user_id == userInfoRequest.id && pb.paid == false).ToListAsync();

        if (priceBets.Count == 0)
        {
          _logger.Log.Warning("[INFO] :: CurrentPriceBets :: Empty list of price bets on userID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User has no current price bets!" });
        }
        _logger.Log.Debug("[INFO] :: CurrentPriceBets :: success with ID: {msg}", userInfoRequest.id);
        return Ok(new { Message = "CurrentPriceBets SUCCESS", PriceBets = priceBets });
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: CurrentPriceBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("HistoricPriceBets")]
    public async Task<IActionResult> HistoricPriceBets([FromBody] idRequest userInfoRequest)
    {
      try
      {
        var priceBets = await _dbContext.PriceBets.Where(pb => pb.user_id == userInfoRequest.id && pb.paid == true).ToListAsync();

        if (priceBets.Count == 0)
        {
          _logger.Log.Warning("[INFO] :: HistoricPriceBets :: Empty list of price bets on userID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User has no historic price bets!" });
        }
        _logger.Log.Debug("[INFO] :: HistoricPriceBets :: success with ID: {msg}", userInfoRequest.id);
        return Ok(new { Message = "HistoricPriceBets SUCCESS", PriceBets = priceBets });
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: HistoricPriceBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("NewPriceBet")]
    public async Task<IActionResult> NewPriceBet([FromBody] newPriceBetRequest priceBetRequest)
    {
      using Microsoft.EntityFrameworkCore.Storage.IDbContextTransaction transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.fcm == priceBetRequest.fcm && u.id == priceBetRequest.user_id);
        var existingBet = await _dbContext.PriceBets.FirstOrDefaultAsync(pb => pb.ticker == priceBetRequest.ticker && pb.end_date == priceBetRequest.end_date);
        int betCost = GetBetCostFromMargin(priceBetRequest.margin);

        if (user == null) throw new Exception("Unexistent user or session expired!");
        if (user.points < betCost) throw new BetException("NO POINTS");
        if (existingBet != null) throw new BetException("EXISTING BET");
        if (priceBetRequest.end_date < DateTime.UtcNow.AddDays(PRICE_BET_DAYS_MARGIN)) throw new BetException("NO TIME");

        var newPriceBet = new PriceBet(user_id: priceBetRequest.user_id!, ticker: priceBetRequest.ticker!,
                                price_bet: priceBetRequest.price_bet, margin: priceBetRequest.margin, end_date: priceBetRequest.end_date);

        if (newPriceBet != null)
        {
          _dbContext.PriceBets.Add(newPriceBet);
          user.points -= betCost;
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Information("[INFO] :: NewPriceBet :: Exact-price bet created successfully for user {0} on ticker {1}", priceBetRequest.user_id, priceBetRequest.ticker);
          return Ok(new { });
        }

        throw new Exception("Unknown error");
      }
      catch (BetException ex)
      {
        if (ex.Message == "NO TIME")
        {
          await transaction.RollbackAsync();
          _logger.Log.Warning("[WARN] :: NewPriceBet :: Not enough time for price bet: {0}", ex.Message);
          return StatusCode(410, new { Message = "Not enough time", Error = ex.Message });

        }

        if (ex.Message == "NO POINTS")
        {
          await transaction.RollbackAsync();
          _logger.Log.Warning("[WARN] :: NewPriceBet :: Not enough points for price bet: {0}", ex.Message);
          return StatusCode(420, new { Message = "Not enough points", Error = ex.Message });

        }

        if (ex.Message == "EXISTING BET")
        {
          await transaction.RollbackAsync();
          _logger.Log.Warning("[WARN] :: NewPriceBet :: Already-existing exact same price bet: {0}", ex.Message);
          return StatusCode(430, new { Message = "Already existing bet", Error = ex.Message });

        }
        else
        {
          await transaction.RollbackAsync();
          _logger.Log.Error("[ERR] :: NewPriceBet :: Unknown exception: ", ex.Message);
          return StatusCode(400, new { Message = "Unknown exception", Error = ex.Message });

        }

      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.Log.Error("[INFO] :: NewPriceBet :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("GetBetZones")]
    public async Task<IActionResult> GetBetZones([FromBody] symbolWithTimeframe ticker)
    {
      
      try
      {
        var betZones = await _dbContext.BetZones.Where(bz => bz.ticker == ticker.id && bz.timeframe == ticker.timeframe && bz.active == true).ToListAsync();

        if (betZones.Count != 0)
        {
          _logger.Log.Debug("[INFO] :: GetBets :: Success on ticker: {msg}", ticker.id);
          return Ok(new { bets = betZones });
        }
        else
        {
          _logger.Log.Debug("[INFO] :: GetBets :: Bets not found for ticker: {msg}", ticker.id);
          return NotFound(new { Message = "No bets found for this ticker" });
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: GetBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
      
    }

    [HttpPost("GetBetZone")]
    public async Task<IActionResult> GetBetZone([FromBody] integerIdRequest betID)
    {
      
      try
      {
        double origin_odds = 1.1;
        var bet = await _dbContext.Bet.FirstOrDefaultAsync(b => b.id == betID.id);
        if (bet != null)
        {
          var betZone = await _dbContext.BetZones.FirstOrDefaultAsync(bz => bz.id == bet.bet_zone);
          var origin_bet = await _dbContext.Bet.FirstOrDefaultAsync(b => b.bet_zone == betID.id);
          if (origin_bet == null)
          {
            origin_odds = bet.origin_odds;
          }
          if (null != betZone)
          {
            _logger.Log.Debug("[INFO] :: GetBetZone :: Success on bet ID: {msg}", betID.id);
            return Ok(new { bets = new List<BetZone> { new(betZone.ticker, betZone.target_value, betZone.bet_margin,
                                                            betZone.start_date, betZone.end_date, origin_odds, betZone.timeframe) } });
          }
          else
          {
            _logger.Log.Warning("[INFO] :: GetBetZone :: Bet Zone doesn't exist!");
            return NotFound(new { Message = "Bet Zone doesn't exist" });
          }
        }
        else
        {
          _logger.Log.Warning("[INFO] :: GetBetZone :: Bet not found for ID: {msg}", betID.id);
          return NotFound(new { Message = "Bet not found for ID" });
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: GetBetZone :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

    }

    [HttpPost("DeleteRecentBet")]
    public async Task<IActionResult> DeleteRecentBet([FromBody] idRequest betIdRequest)
    {
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var bet = await _dbContext.Bet.FirstOrDefaultAsync(u => u.id.ToString() == betIdRequest.id);

        if (bet != null)
        {
          _dbContext.Bet.Remove(bet);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Debug("[INFO] :: DeleteRecentBet :: Bet removed successfully with ID: {msg}", betIdRequest.id);
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

    [HttpPost("DeleteRecentPriceBet")]
    public async Task<IActionResult> DeleteRecentPriceBet([FromBody] idRequest betIdRequest)
    {
      using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var priceBet = await _dbContext.PriceBets.FirstOrDefaultAsync(u => u.id.ToString() == betIdRequest.id);

        if (priceBet != null)
        {
          _dbContext.PriceBets.Remove(priceBet);
          await _dbContext.SaveChangesAsync();
          await transaction.CommitAsync();
          _logger.Log.Debug("[INFO] :: DeleteRecentPriceBet :: Bet removed successfully with ID: {msg}", betIdRequest.id);
          return Ok(new { });
        }
        else
        {
          await transaction.RollbackAsync();
          _logger.Log.Warning("[INFO] :: DeleteRecentPriceBet :: Bet not found for ID: {msg}", betIdRequest.id);
          return NotFound(new { Message = "Bet not found" });
        }
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.Log.Error("[INFO] :: DeleteRecentPriceBet :: Server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("DeleteHistoricBet")]
    public async Task<IActionResult> DeleteHistoricBet([FromBody] idRequest userInfoRequestId)
    {
      await using var transaction = await _dbContext.Database.BeginTransactionAsync();
      try
      {
        var bets = await _dbContext.Bet
            .Where(b => b.user_id == userInfoRequestId.id &&
                        _dbContext.BetZones.Any(bz => bz.id == b.bet_zone && bz.end_date < DateTime.UtcNow))
            .ToListAsync();

        var priceBets = await _dbContext.PriceBets
            .Where(pb => pb.user_id == userInfoRequestId.id && pb.end_date < DateTime.UtcNow)
            .ToListAsync();

        if ((bets == null || bets.Count == 0) && (priceBets == null || priceBets.Count == 0))
        {
          _logger.Log.Warning("[INFO] :: DeleteHistoricBet :: No bets found for user: {msg}", userInfoRequestId.id);
          return NotFound(new { Message = "No bets found for the user" });
        }

        if (bets != null && bets.Count > 0)
        {
          _dbContext.Bet.RemoveRange(bets);
          _logger.Log.Debug("[INFO] :: DeleteHistoricBet :: Bets removed successfully for user: {msg}", userInfoRequestId.id);
        }

        if (priceBets != null && priceBets.Count > 0)
        {
          _dbContext.PriceBets.RemoveRange(priceBets);
          _logger.Log.Debug("[INFO] :: DeleteHistoricBet :: PriceBets removed successfully for user: {msg}", userInfoRequestId.id);
        }

        await _dbContext.SaveChangesAsync();
        await transaction.CommitAsync();

        return Ok(new { Message = "All old bets deleted successfully" });
      }
      catch (Exception ex)
      {
        await transaction.RollbackAsync();
        _logger.Log.Error(ex, "[INFO] :: DeleteHistoricBet :: Server error for user: {msg}", userInfoRequestId.id);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    #region Private Methods
    private int GetBetCostFromMargin(double margin)
    {
      return margin switch
      {
        0.0 => PRICE_BET_COST_0_MARGIN,
        0.01 => PRICE_BET_COST_1_MARGIN,
        0.05 => PRICE_BET_COST_5_MARGIN,
        0.075 => PRICE_BET_COST_7_MARGIN,
        0.1 => PRICE_BET_COST_10_MARGIN,
        _ => PRICE_BET_COST_0_MARGIN,
      };
    }

    #endregion

  }

  public class BetException(string msg) : Exception(msg)
  {
  }

}
