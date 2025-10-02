using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class DiditController : ControllerBase
  {
    private readonly AppDbContext _dbContext;
    private readonly ICustomLogger _logger;
    private readonly IConfiguration _config;

    public DiditController(AppDbContext dbContext, ICustomLogger customLogger, IConfiguration config)
    {
      _dbContext = dbContext;
      _logger = customLogger;
      _config = config;
    }

    [HttpPost("CreateSession")]
    public async Task<IActionResult> CreateSession([FromBody] IdRequest req)
    {
      try
      {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.id == req.id);
        if (user == null) return NotFound(new { Message = "User not found" });

        var apiKey = Environment.GetEnvironmentVariable("DIDIT_API_KEY", EnvironmentVariableTarget.User) ?? "";
        var workflowId = Environment.GetEnvironmentVariable("DIDIT_WORKFLOW_ID", EnvironmentVariableTarget.User) ?? "";
        var callbackUrl = "https://api.betstrading.online/api/Didit/Webhook";

        using var http = new HttpClient();
        http.DefaultRequestHeaders.Add("x-api-key", apiKey);

        var payload = new
        {
          workflow_id = workflowId,
          vendor_data = user.id,
          callback = callbackUrl
        };

        var response = await http.PostAsync(
          "https://verification.didit.me/v2/session/",
          new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
          _logger.Log.Error("[DIDIT] :: Error creating session for user {id}: {body}", user.id, body);
          return StatusCode((int)response.StatusCode, body);
        }

        _logger.Log.Information("[DIDIT] :: Session created for user {id}", user.id);
        return Ok(JsonSerializer.Deserialize<JsonElement>(body));
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[DIDIT] :: Exception creating session");
        return StatusCode(500, new { Message = "Internal server error" });
      }
    }

    [AllowAnonymous]
    [HttpPost("Webhook")]
    public async Task<IActionResult> Webhook()
    {
      try
      {
        using var reader = new StreamReader(Request.Body);
        var json = await reader.ReadToEndAsync();

        var payload = JsonSerializer.Deserialize<JsonElement>(json);

        if (!payload.TryGetProperty("decision", out var decision))
        {
          _logger.Log.Warning("[DIDIT] :: Webhook payload without decision node: {json}", json);
          return BadRequest();
        }

        string? vendorData = null;
        if (decision.TryGetProperty("vendor_data", out var vendorProp))
        {
          vendorData = vendorProp.GetString();
        }

        if (string.IsNullOrEmpty(vendorData))
        {
          _logger.Log.Warning("[DIDIT] :: Webhook without vendor_data: {json}", json);
          return BadRequest();
        }

        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.id == vendorData);
        if (user == null)
        {
          _logger.Log.Warning("[DIDIT] :: Webhook for non-existent user {vendor}", vendorData);
          return NotFound();
        }

        double score = 0;
        if (decision.TryGetProperty("face_match", out var faceMatch) &&
            faceMatch.TryGetProperty("score", out var scoreProp))
        {
          score = scoreProp.GetDouble();
        }

        if (score >= 75)
        {
          user.is_verified = true;
          await _dbContext.SaveChangesAsync();
          _logger.Log.Information("[DIDIT] :: User {id} verified with score {score}", user.id, score);
        }
        else
        {
          _logger.Log.Warning("[DIDIT] :: User {id} verification failed, score {score}", user.id, score);
        }

        return Ok(new { Message = "Webhook processed" });
      }
      catch (Exception ex)
      {
        _logger.Log.Error(ex, "[DIDIT] :: Webhook error");
        return StatusCode(500, new { Message = "Internal server error" });
      }
    }

  }

  public class IdRequest
  {
    public string? id { get; set; }
  }
}
