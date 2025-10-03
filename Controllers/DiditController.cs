using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Net.Http.Headers;
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
        //TODO
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
          //TODO
            "https://verification.didit.me/v2/session/",
            new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        );

        var body = await response.Content.ReadAsStringAsync();

        if (!response.IsSuccessStatusCode)
        {
          _logger.Log.Error("[DIDIT] :: Error creating session for user {id}: {body}", user.id, body);
          return StatusCode((int)response.StatusCode, body);
        }

        var json = JsonSerializer.Deserialize<JsonElement>(body);

        if (json.TryGetProperty("session_id", out var idProp))
        {
          var sessionId = idProp.GetString();
          user.didit_session_id = sessionId ?? null;
          await _dbContext.SaveChangesAsync();

          _logger.Log.Information("[DIDIT] :: Session {sid} created for user {id}", sessionId, user.id);
        }

        return Ok(json);
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
        
        string? vendorData = payload.TryGetProperty("vendor_data", out var vendorProp)
            ? vendorProp.GetString()
            : null;

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
        
        if (payload.TryGetProperty("session_id", out var sidProp))
        {
          var sessionId = sidProp.GetString();
          if (!string.IsNullOrEmpty(sessionId))
          {
            user.didit_session_id = sessionId;
            await _dbContext.SaveChangesAsync();
            _logger.Log.Information("[DIDIT] :: Updated user {id} with session {sid}", user.id, sessionId);
          }
        }
        
        string webhookType = payload.TryGetProperty("webhook_type", out var wtProp)
            ? wtProp.GetString() ?? ""
            : "";

        string status = payload.TryGetProperty("status", out var stProp)
            ? stProp.GetString() ?? ""
            : "";

        if (webhookType == "status.updated")
        {
          _logger.Log.Information("[DIDIT] :: Status update for user {id}: {status}", user.id, status);
          
          if (status == "Approved" || status == "Declined")
          {
            if (!string.IsNullOrEmpty(user.didit_session_id))
            {
              using var http = new HttpClient();
              http.DefaultRequestHeaders.Add("x-api-key",
                  Environment.GetEnvironmentVariable("DIDIT_API_KEY", EnvironmentVariableTarget.User) ?? "");

              //TODO
              var response = await http.GetAsync($"https://verification.didit.me/v2/session/{user.didit_session_id}/decision");
              if (response.IsSuccessStatusCode)
              {
                var body = await response.Content.ReadAsStringAsync();
                var sessionJson = JsonSerializer.Deserialize<JsonElement>(body);

                if (sessionJson.TryGetProperty("id_verification", out var idVer))
                {
                  
                  if (idVer.TryGetProperty("date_of_birth", out var dobProp))
                  {
                    var dobStr = dobProp.GetString();
                    if (!string.IsNullOrEmpty(dobStr) && DateTime.TryParse(dobStr, out var dob))
                    {
                      user.birthday = dob;
                      _logger.Log.Information("[DIDIT] :: User {id} DOB set to {dob}", user.id, dob);
                    }
                  }
                  
                  if (status == "Approved")
                  {
                    user.is_verified = true;
                    _logger.Log.Information("[DIDIT] :: User {id} verified", user.id);
                  }
                  else
                  {
                    user.is_verified = false;
                    _logger.Log.Warning("[DIDIT] :: User {id} verification declined", user.id);
                  }
                }
              }
              else
              {
                _logger.Log.Warning("[DIDIT] :: Failed to fetch session {sid}, status {status}",
                    user.didit_session_id, response.StatusCode);
              }
            }
          }
        }

        await _dbContext.SaveChangesAsync();

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
