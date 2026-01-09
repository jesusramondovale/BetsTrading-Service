using BetsTrading_Service.Database;
using BetsTrading_Service.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Cryptography;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class RewardsController(AppDbContext db) : ControllerBase
  {
    private readonly AppDbContext _db = db;

    static string NewBase64UrlNonce(int bytes = 32)
    {
      var buffer = RandomNumberGenerator.GetBytes(bytes);
      return Convert.ToBase64String(buffer)
          .Replace('+', '-').Replace('/', '_').TrimEnd('=');
    }

    [HttpPost("RequestAdNonce")]
    [Consumes("application/json")]
    public async Task<ActionResult<RewardNonceResponse>> RequestNonce([FromBody] RewardNonceRequest body)
    {
      if (string.IsNullOrWhiteSpace(body?.AdUnitId))
        return BadRequest("ad_unit_id required");

      var userId = Request.Headers["X-UserId"].ToString();
      if (string.IsNullOrWhiteSpace(userId))
        return Unauthorized("missing user id");


      var now = DateTime.UtcNow;
      var expired = await _db.RewardNonces
          .Where(n => n.ExpiresAt < now && !n.Used)
          .ToListAsync();
      if (expired.Count > 0)
      {
        _db.RewardNonces.RemoveRange(expired);
        await _db.SaveChangesAsync();
      }

      var outstanding = await _db.RewardNonces
          .CountAsync(n => n.UserId == userId && !n.Used && n.ExpiresAt >= now);
      if (outstanding >= 3)
        return StatusCode(429, "too_many_pending_nonces");

      var nonce = NewBase64UrlNonce(24);
      var entity = new RewardNonce
      {
        Nonce = nonce,
        UserId = userId,
        AdUnitId = body.AdUnitId,
        Purpose = body.Purpose,
        Coins = body.Coins,
        Used = false,
        CreatedAt = now,
        ExpiresAt = now.AddMinutes(5)
      };

      _db.RewardNonces.Add(entity);
      await _db.SaveChangesAsync();

      return Ok(new RewardNonceResponse
      {
        Nonce = nonce,
        ExpiresAt = entity.ExpiresAt
      });
    }
  }
}
