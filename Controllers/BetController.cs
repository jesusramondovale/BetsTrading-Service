using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using BetsTrading_Service.Requests;
using Microsoft.AspNetCore.Mvc;

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
    public IActionResult UserBets([FromBody] idRequest userInfoRequest)
    {
      try
      {
        var bets = _dbContext.Bet.Where(bet => bet.user_id == userInfoRequest.id).ToList();

        if (!(bets.Any()))
        {
          _logger.Log.Warning("[INFO] :: UserBets :: Empty list of bets on userID: {msg}", userInfoRequest.id);
          return NotFound(new { Message = "User has no bets!" });
        }

        _logger.Log.Information("[INFO] :: UserBets :: success with ID: {msg}", userInfoRequest.id);
        List<BetDTO> betDTOs = new List<BetDTO>();

        foreach (var bet in bets)
        {
          var tmpAsset = _dbContext.FinancialAssets.Where(a => a.ticker == bet.ticker).FirstOrDefault();

          var tmpBetZone = _dbContext.BetZones.Where(bz => bz.id == bet.bet_zone).FirstOrDefault();
          TimeSpan timeMargin = (TimeSpan)(tmpBetZone!.end_date - tmpBetZone!.start_date)!;

          betDTOs.Add(new BetDTO(user_id: userInfoRequest.id!, ticker: bet.ticker, name: tmpAsset!.name!, bet_amount: bet.bet_amount,
            origin_value: bet.origin_value, current_value: tmpAsset.current, target_value: tmpBetZone.target_value, target_margin: tmpBetZone.bet_margin, target_date: tmpBetZone.start_date, target_odds: tmpBetZone.target_odds,
            target_won: bet.target_won, icon_path: tmpAsset.icon!, type: tmpBetZone.type, date_margin: timeMargin.Days));

        }

        return Ok(new
        {
          Message = "UserBets SUCCESS",
          Bets = betDTOs
        });


      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: UserBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }
    }

    [HttpPost("NewBet")]
    public IActionResult NewBet([FromBody] newBetRequest betRequest)
    {

      try
      {

        var betZone = _dbContext.BetZones.Where(bz => bz.id == betRequest.bet_zone).FirstOrDefault();


        var newBet = new Bet(user_id: betRequest.user_id!, ticker: betRequest.ticker!, bet_amount: betRequest.bet_amount, 
                             origin_value: betRequest.origin_value, target_value: betZone!.target_value, target_margin: betZone.bet_margin, 
                             target_won: false, bet_zone: betRequest.bet_zone);
        
        
        if (null != newBet)
        {
          _dbContext.Bet.Add(newBet);
          _dbContext.SaveChanges();
          return Ok(new { });

        }
        else if (null == betZone) { 
          throw new Exception("Unexistent bet zone"); 
        }
        
        throw new Exception("Unknown error");

      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: NewBet :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }


    }

    [HttpPost("GetBetZones")]
    public IActionResult GetBetZones([FromBody] idRequest ticker)
    {

      try
      {
        var betZones = _dbContext.BetZones.Where(bz => bz.ticker == ticker.id).ToList();

        if (betZones.Any())
        {

          _logger.Log.Information("[INFO] :: GetBets :: Success on ticker: {msg}", ticker.id);
          return Ok(new
          {
            bets = betZones

          });
        }
        else // Unexistent zones for given ticker
        {
          _logger.Log.Warning("[INFO] :: GetBets :: Bets not found for ticker: {msg}", ticker.id);
          return NotFound(new { Message = "No bets found for this ticker" }); // Ticker not found
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: GetBets :: Internal server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }

    }

    [HttpPost("DeleteRecentBet")]
    public IActionResult DeleteRecentBet([FromBody] idRequest betIdRequest)
    {

      try
      {
        var bet = _dbContext.Bet
            .FirstOrDefault(u => u.id.ToString() == betIdRequest.id);

        if (bet != null) // Bet exists
        {
          _dbContext.Bet.Remove(bet);
          _dbContext.SaveChanges();
          _logger.Log.Warning("[INFO] :: DeleteRecentBet :: Bet removed succesfuly with ID: {msg}", betIdRequest.id);
          return Ok(new { });

        }

        else // Bet not found
        {
          _logger.Log.Warning("[INFO] :: DeleteRecentBet :: Bet not found for ID: {msg}", betIdRequest.id);
          return NotFound(new { Message = "User or email not found" });
        }
      }
      catch (Exception ex)
      {
        _logger.Log.Error("[INFO] :: UserInfo :: DeleteRecentBet server error: {msg}", ex.Message);
        return StatusCode(500, new { Message = "Server error", Error = ex.Message });
      }



    }



  }
}
