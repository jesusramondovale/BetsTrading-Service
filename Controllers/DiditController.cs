using BetsTrading_Service.Database;
using BetsTrading_Service.Interfaces;
using BetsTrading_Service.Locale;
using BetsTrading_Service.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace BetsTrading_Service.Controllers
{
  [ApiController]
  [Route("api/[controller]")]
  public class DiditController(AppDbContext dbContext, ICustomLogger customLogger, IEmailService emailService) : ControllerBase
  {
    private readonly AppDbContext _dbContext = dbContext;
    private readonly ICustomLogger _logger = customLogger;
    private readonly IEmailService _emailService = emailService;

    [HttpPost("CreateSession")]
    public async Task<IActionResult> CreateSession([FromBody] IdRequest req)
    {
      try
      {
        var user = await _dbContext.Users.FirstOrDefaultAsync(u => u.id == req.id);
        if (user == null) return NotFound(new { Message = "User not found" });

        var apiKey = Environment.GetEnvironmentVariable("DIDIT_API_KEY") ?? "";
        var workflowId = Environment.GetEnvironmentVariable("DIDIT_WORKFLOW_ID") ?? "";
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

          _logger.Log.Debug("[DIDIT] :: Session {sid} created for user {id}", sessionId, user.id);
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
            _logger.Log.Debug("[DIDIT] :: Updated user {id} with session {sid}", user.id, sessionId);
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
          _logger.Log.Debug("[DIDIT] :: Status update for user {id}: {status}", user.id, status);
          
          if (status == "Approved" || status == "Declined")
          {
            if (!string.IsNullOrEmpty(user.didit_session_id))
            {
              using var http = new HttpClient();
              http.DefaultRequestHeaders.Add("x-api-key",
                  Environment.GetEnvironmentVariable("DIDIT_API_KEY") ?? "");

              var response = await http.GetAsync($"https://verification.didit.me/v2/session/{user.didit_session_id}/decision");
              if (response.IsSuccessStatusCode)
              {
                var body = await response.Content.ReadAsStringAsync();
                var sessionJson = JsonSerializer.Deserialize<JsonElement>(body);

                if (sessionJson.TryGetProperty("id_verification", out var idVer))
                {

                  if (idVer.TryGetProperty("age", out var age))
                  {
                    var ageInt = age.GetInt16();
                    if (ageInt < 18)
                    {
                      _logger.Log.Warning("[DIDIT] :: User {id} is below legal age ({ageInt} y-o)",user.id ,ageInt);
                      return StatusCode(500, new { Message = "User under legal age" });
                    }

                  }

                  if (idVer.TryGetProperty("full_name", out var fullName))
                  {
                    var fullNameStr = fullName.GetString();
                    if (!string.IsNullOrEmpty(fullNameStr))
                    {
                      user.fullname = fullNameStr;
                      _logger.Log.Information("[DIDIT] :: User {id} full name set to {fullNameStr}", user.id, fullNameStr);
                    }
                  }

                  if (idVer.TryGetProperty("issuing_state_name", out var country))
                  {
                    var countryStr = country.GetString();
                    if (!string.IsNullOrEmpty(countryStr))
                    {
                      user.country = GetCountryCodeByName(countryStr);
                      _logger.Log.Information("[DIDIT] :: User {id} country set to {countryStr}", user.id, countryStr);
                    }
                  }


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

                    string localedBodyTemplate = LocalizedTexts.GetTranslationByCountry(user.country ?? "UK", "userVerifiedEmailBody");
                    string localedBody = string.Format(localedBodyTemplate, user.fullname);

                    await _emailService.SendEmailAsync(
                        to: user.email,
                        subject: LocalizedTexts.GetTranslationByCountry(user.country ?? "UK", "emailSubjectUserVerified"),
                        body: localedBody
                    );

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
                _logger.Log.Warning("[DIDIT] :: Failed to fetch session {sid}, status {status}", user.didit_session_id, response.StatusCode);
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

    #region Private methods

    public static List<Dictionary<string, string>> GetTopCountries()
    {
      return
            [
                new() { ["name"] = "Afghanistan", ["code"] = "AF" },
                new() { ["name"] = "Albania", ["code"] = "AL" },
                new() { ["name"] = "Algeria", ["code"] = "DZ" },
                new() { ["name"] = "American Samoa", ["code"] = "AS" },
                new() { ["name"] = "Andorra", ["code"] = "AD" },
                new() { ["name"] = "Angola", ["code"] = "AO" },
                new() { ["name"] = "Anguilla", ["code"] = "AI" },
                new() { ["name"] = "Antarctica", ["code"] = "AQ" },
                new() { ["name"] = "Antigua-Barbuda", ["code"] = "AG" },
                new() { ["name"] = "Argentina", ["code"] = "AR" },
                new() { ["name"] = "Armenia", ["code"] = "AM" },
                new() { ["name"] = "Aruba", ["code"] = "AW" },
                new() { ["name"] = "Australia", ["code"] = "AU" },
                new() { ["name"] = "Austria", ["code"] = "AT" },
                new() { ["name"] = "Azerbaijan", ["code"] = "AZ" },
                new() { ["name"] = "Bahamas", ["code"] = "BS" },
                new() { ["name"] = "Bahrain", ["code"] = "BH" },
                new() { ["name"] = "Bangladesh", ["code"] = "BD" },
                new() { ["name"] = "Barbados", ["code"] = "BB" },
                new() { ["name"] = "Belarus", ["code"] = "BY" },
                new() { ["name"] = "Belgium", ["code"] = "BE" },
                new() { ["name"] = "Belize", ["code"] = "BZ" },
                new() { ["name"] = "Benin", ["code"] = "BJ" },
                new() { ["name"] = "Bermuda", ["code"] = "BM" },
                new() { ["name"] = "Bhutan", ["code"] = "BT" },
                new() { ["name"] = "Bolivia", ["code"] = "BO" },
                new() { ["name"] = "Bonaire", ["code"] = "BQ" },
                new() { ["name"] = "Bosnia", ["code"] = "BA" },
                new() { ["name"] = "Botswana", ["code"] = "BW" },
                new() { ["name"] = "Bouvet Island", ["code"] = "BV" },
                new() { ["name"] = "Brazil", ["code"] = "BR" },
                new() { ["name"] = "British Indian", ["code"] = "IO" },
                new() { ["name"] = "Brunei Darussalam", ["code"] = "BN" },
                new() { ["name"] = "Bulgaria", ["code"] = "BG" },
                new() { ["name"] = "Burkina Faso", ["code"] = "BF" },
                new() { ["name"] = "Burundi", ["code"] = "BI" },
                new() { ["name"] = "Cabo Verde", ["code"] = "CV" },
                new() { ["name"] = "Cambodia", ["code"] = "KH" },
                new() { ["name"] = "Cameroon", ["code"] = "CM" },
                new() { ["name"] = "Canada", ["code"] = "CA" },
                new() { ["name"] = "Cayman Islands", ["code"] = "KY" },
                new() { ["name"] = "Chad", ["code"] = "TD" },
                new() { ["name"] = "Chile", ["code"] = "CL" },
                new() { ["name"] = "China", ["code"] = "CN" },
                new() { ["name"] = "Christmas Island", ["code"] = "CX" },
                new() { ["name"] = "Cocos Islands", ["code"] = "CC" },
                new() { ["name"] = "Colombia", ["code"] = "CO" },
                new() { ["name"] = "Comoros", ["code"] = "KM" },
                new() { ["name"] = "Congo", ["code"] = "CD" },
                new() { ["name"] = "Congo", ["code"] = "CG" },
                new() { ["name"] = "Cook Islands", ["code"] = "CK" },
                new() { ["name"] = "Costa Rica", ["code"] = "CR" },
                new() { ["name"] = "Croatia", ["code"] = "HR" },
                new() { ["name"] = "Cuba", ["code"] = "CU" },
                new() { ["name"] = "Curaçao", ["code"] = "CW" },
                new() { ["name"] = "Cyprus", ["code"] = "CY" },
                new() { ["name"] = "Czechia", ["code"] = "CZ" },
                new() { ["name"] = "Côte d'Ivoire", ["code"] = "CI" },
                new() { ["name"] = "Denmark", ["code"] = "DK" },
                new() { ["name"] = "Djibouti", ["code"] = "DJ" },
                new() { ["name"] = "Dominica", ["code"] = "DM" },
                new() { ["name"] = "Dominican Republic", ["code"] = "DO" },
                new() { ["name"] = "Ecuador", ["code"] = "EC" },
                new() { ["name"] = "Egypt", ["code"] = "EG" },
                new() { ["name"] = "El Salvador", ["code"] = "SV" },
                new() { ["name"] = "Equatorial Guinea", ["code"] = "GQ" },
                new() { ["name"] = "Eritrea", ["code"] = "ER" },
                new() { ["name"] = "Estonia", ["code"] = "EE" },
                new() { ["name"] = "Eswatini", ["code"] = "SZ" },
                new() { ["name"] = "Ethiopia", ["code"] = "ET" },
                new() { ["name"] = "Falkland Islands", ["code"] = "FK" },
                new() { ["name"] = "Faroe Islands", ["code"] = "FO" },
                new() { ["name"] = "Fiji", ["code"] = "FJ" },
                new() { ["name"] = "Finland", ["code"] = "FI" },
                new() { ["name"] = "France", ["code"] = "FR" },
                new() { ["name"] = "French Guiana", ["code"] = "GF" },
                new() { ["name"] = "French Polynesia", ["code"] = "PF" },
                new() { ["name"] = "French Southern", ["code"] = "TF" },
                new() { ["name"] = "Gabon", ["code"] = "GA" },
                new() { ["name"] = "Gambia", ["code"] = "GM" },
                new() { ["name"] = "Georgia", ["code"] = "GE" },
                new() { ["name"] = "Germany", ["code"] = "DE" },
                new() { ["name"] = "Ghana", ["code"] = "GH" },
                new() { ["name"] = "Gibraltar", ["code"] = "GI" },
                new() { ["name"] = "Greece", ["code"] = "GR" },
                new() { ["name"] = "Greenland", ["code"] = "GL" },
                new() { ["name"] = "Grenada", ["code"] = "GD" },
                new() { ["name"] = "Guadeloupe", ["code"] = "GP" },
                new() { ["name"] = "Guam", ["code"] = "GU" },
                new() { ["name"] = "Guatemala", ["code"] = "GT" },
                new() { ["name"] = "Guernsey", ["code"] = "GG" },
                new() { ["name"] = "Guinea", ["code"] = "GN" },
                new() { ["name"] = "Guinea-Bissau", ["code"] = "GW" },
                new() { ["name"] = "Guyana", ["code"] = "GY" },
                new() { ["name"] = "Haiti", ["code"] = "HT" },
                new() { ["name"] = "McDonald Islands", ["code"] = "HM" },
                new() { ["name"] = "Holy See", ["code"] = "VA" },
                new() { ["name"] = "Honduras", ["code"] = "HN" },
                new() { ["name"] = "Hong Kong", ["code"] = "HK" },
                new() { ["name"] = "Hungary", ["code"] = "HU" },
                new() { ["name"] = "Iceland", ["code"] = "IS" },
                new() { ["name"] = "India", ["code"] = "IN" },
                new() { ["name"] = "Indonesia", ["code"] = "ID" },
                new() { ["name"] = "Iran", ["code"] = "IR" },
                new() { ["name"] = "Iraq", ["code"] = "IQ" },
                new() { ["name"] = "Ireland", ["code"] = "IE" },
                new() { ["name"] = "Isle of Man", ["code"] = "IM" },
                new() { ["name"] = "Israel", ["code"] = "IL" },
                new() { ["name"] = "Italy", ["code"] = "IT" },
                new() { ["name"] = "Jamaica", ["code"] = "JM" },
                new() { ["name"] = "Japan", ["code"] = "JP" },
                new() { ["name"] = "Jersey", ["code"] = "JE" },
                new() { ["name"] = "Jordan", ["code"] = "JO" },
                new() { ["name"] = "Kazakhstan", ["code"] = "KZ" },
                new() { ["name"] = "Kenya", ["code"] = "KE" },
                new() { ["name"] = "Kiribati", ["code"] = "KI" },
                new() { ["name"] = "Korea (North)", ["code"] = "KP" },
                new() { ["name"] = "Korea (South)", ["code"] = "KR" },
                new() { ["name"] = "Kuwait", ["code"] = "KW" },
                new() { ["name"] = "Kyrgyzstan", ["code"] = "KG" },
                new() { ["name"] = "Laos", ["code"] = "LA" },
                new() { ["name"] = "Latvia", ["code"] = "LV" },
                new() { ["name"] = "Lebanon", ["code"] = "LB" },
                new() { ["name"] = "Lesotho", ["code"] = "LS" },
                new() { ["name"] = "Liberia", ["code"] = "LR" },
                new() { ["name"] = "Libya", ["code"] = "LY" },
                new() { ["name"] = "Liechtenstein", ["code"] = "LI" },
                new() { ["name"] = "Lithuania", ["code"] = "LT" },
                new() { ["name"] = "Luxembourg", ["code"] = "LU" },
                new() { ["name"] = "Macao", ["code"] = "MO" },
                new() { ["name"] = "Madagascar", ["code"] = "MG" },
                new() { ["name"] = "Malawi", ["code"] = "MW" },
                new() { ["name"] = "Malaysia", ["code"] = "MY" },
                new() { ["name"] = "Maldives", ["code"] = "MV" },
                new() { ["name"] = "Mali", ["code"] = "ML" },
                new() { ["name"] = "Malta", ["code"] = "MT" },
                new() { ["name"] = "Marshall Islands", ["code"] = "MH" },
                new() { ["name"] = "Martinique", ["code"] = "MQ" },
                new() { ["name"] = "Mauritania", ["code"] = "MR" },
                new() { ["name"] = "Mauritius", ["code"] = "MU" },
                new() { ["name"] = "Mexico", ["code"] = "MX" },
                new() { ["name"] = "Micronesia", ["code"] = "FM" },
                new() { ["name"] = "Moldova", ["code"] = "MD" },
                new() { ["name"] = "Monaco", ["code"] = "MC" },
                new() { ["name"] = "Mongolia", ["code"] = "MN" },
                new() { ["name"] = "Montenegro", ["code"] = "ME" },
                new() { ["name"] = "Montserrat", ["code"] = "MS" },
                new() { ["name"] = "Morocco", ["code"] = "MA" },
                new() { ["name"] = "Mozambique", ["code"] = "MZ" },
                new() { ["name"] = "Myanmar", ["code"] = "MM" },
                new() { ["name"] = "Namibia", ["code"] = "NA" },
                new() { ["name"] = "Netherlands", ["code"] = "NL" },
                new() { ["name"] = "New Caledonia", ["code"] = "NC" },
                new() { ["name"] = "New Zealand", ["code"] = "NZ" },
                new() { ["name"] = "Nicaragua", ["code"] = "NI" },
                new() { ["name"] = "Niger", ["code"] = "NE" },
                new() { ["name"] = "Nigeria", ["code"] = "NG" },
                new() { ["name"] = "Niue", ["code"] = "NU" },
                new() { ["name"] = "Norfolk Island", ["code"] = "NF" },
                new() { ["name"] = "Northern Mariana", ["code"] = "MP" },
                new() { ["name"] = "Norway", ["code"] = "NO" },
                new() { ["name"] = "Oman", ["code"] = "OM" },
                new() { ["name"] = "Pakistan", ["code"] = "PK" },
                new() { ["name"] = "Palau", ["code"] = "PW" },
                new() { ["name"] = "Palestine", ["code"] = "PS" },
                new() { ["name"] = "Panama", ["code"] = "PA" },
                new() { ["name"] = "Papua New Guinea", ["code"] = "PG" },
                new() { ["name"] = "Paraguay", ["code"] = "PY" },
                new() { ["name"] = "Peru", ["code"] = "PE" },
                new() { ["name"] = "Philippines", ["code"] = "PH" },
                new() { ["name"] = "Pitcairn", ["code"] = "PN" },
                new() { ["name"] = "Poland", ["code"] = "PL" },
                new() { ["name"] = "Portugal", ["code"] = "PT" },
                new() { ["name"] = "Puerto Rico", ["code"] = "PR" },
                new() { ["name"] = "Qatar", ["code"] = "QA" },
                new() { ["name"] = "North Macedonia", ["code"] = "MK" },
                new() { ["name"] = "Romania", ["code"] = "RO" },
                new() { ["name"] = "Russia", ["code"] = "RU" },
                new() { ["name"] = "Rwanda", ["code"] = "RW" },
                new() { ["name"] = "Réunion", ["code"] = "RE" },
                new() { ["name"] = "Saint Barthélemy", ["code"] = "BL" },
                new() { ["name"] = "Saint Helena", ["code"] = "SH" },
                new() { ["name"] = "Saint Kitts and Nevis", ["code"] = "KN" },
                new() { ["name"] = "Saint Lucia", ["code"] = "LC" },
                new() { ["name"] = "Saint Martin", ["code"] = "MF" },
                new() { ["name"] = "Samoa", ["code"] = "WS" },
                new() { ["name"] = "San Marino", ["code"] = "SM" },
                new() { ["name"] = "Saudi Arabia", ["code"] = "SA" },
                new() { ["name"] = "Senegal", ["code"] = "SN" },
                new() { ["name"] = "Serbia", ["code"] = "RS" },
                new() { ["name"] = "Seychelles", ["code"] = "SC" },
                new() { ["name"] = "Sierra Leone", ["code"] = "SL" },
                new() { ["name"] = "Singapore", ["code"] = "SG" },
                new() { ["name"] = "Sint Maarten", ["code"] = "SX" },
                new() { ["name"] = "Slovakia", ["code"] = "SK" },
                new() { ["name"] = "Slovenia", ["code"] = "SI" },
                new() { ["name"] = "Solomon Islands", ["code"] = "SB" },
                new() { ["name"] = "Somalia", ["code"] = "SO" },
                new() { ["name"] = "South Africa", ["code"] = "ZA" },
                new() { ["name"] = "South Georgia", ["code"] = "GS" },
                new() { ["name"] = "South Sudan", ["code"] = "SS" },
                new() { ["name"] = "Spain", ["code"] = "ES" },
                new() { ["name"] = "Sri Lanka", ["code"] = "LK" },
                new() { ["name"] = "Sudan", ["code"] = "SD" },
                new() { ["name"] = "Suriname", ["code"] = "SR" },
                new() { ["name"] = "Sweden", ["code"] = "SE" },
                new() { ["name"] = "Switzerland", ["code"] = "CH" },
                new() { ["name"] = "Syria", ["code"] = "SY" },
                new() { ["name"] = "Taiwan", ["code"] = "TW" },
                new() { ["name"] = "Tajikistan", ["code"] = "TJ" },
                new() { ["name"] = "Tanzania", ["code"] = "TZ" },
                new() { ["name"] = "Thailand", ["code"] = "TH" },
                new() { ["name"] = "Timor-Leste", ["code"] = "TL" },
                new() { ["name"] = "Togo", ["code"] = "TG" },
                new() { ["name"] = "Tokelau", ["code"] = "TK" },
                new() { ["name"] = "Tonga", ["code"] = "TO" },
                new() { ["name"] = "Trinidad and Tobago", ["code"] = "TT" },
                new() { ["name"] = "Tunisia", ["code"] = "TN" },
                new() { ["name"] = "Turkey", ["code"] = "TR" },
                new() { ["name"] = "Turkmenistan", ["code"] = "TM" },
                new() { ["name"] = "Turks and Caicos Islands", ["code"] = "TC" },
                new() { ["name"] = "Tuvalu", ["code"] = "TV" },
                new() { ["name"] = "Uganda", ["code"] = "UG" },
                new() { ["name"] = "Ukraine", ["code"] = "UA" },
                new() { ["name"] = "United Arab Emirates", ["code"] = "AE" },
                new() { ["name"] = "United Kingdom", ["code"] = "GB" },
                new() { ["name"] = "United States Islands", ["code"] = "UM" },
                new() { ["name"] = "United States of America", ["code"] = "US" },
                new() { ["name"] = "Uruguay", ["code"] = "UY" },
                new() { ["name"] = "Uzbekistan", ["code"] = "UZ" },
                new() { ["name"] = "Vanuatu", ["code"] = "VU" },
                new() { ["name"] = "Venezuela", ["code"] = "VE" },
                new() { ["name"] = "Viet Nam", ["code"] = "VN" },
                new() { ["name"] = "Virgin Islands (British)", ["code"] = "VG" },
                new() { ["name"] = "Virgin Islands (U.S.)", ["code"] = "VI" },
                new() { ["name"] = "Wallis and Futuna", ["code"] = "WF" },
                new() { ["name"] = "Western Sahara", ["code"] = "EH" },
                new() { ["name"] = "Yemen", ["code"] = "YE" },
                new() { ["name"] = "Zambia", ["code"] = "ZM" },
                new() { ["name"] = "Zimbabwe", ["code"] = "ZW" },
                new() { ["name"] = "Åland Islands", ["code"] = "AX" },
            ];
    }

    public static string GetCountryCodeByName(string name)
    {

      var match = GetTopCountries().FirstOrDefault(c => c.TryGetValue("name", out var countryName) 
                && countryName.Equals(name, System.StringComparison.OrdinalIgnoreCase));

      if (match != null && match.TryGetValue("code", out var code))
        return code;

      return "UNKNWN";
    }

    #endregion

  }

  public class IdRequest
  {
    public string? id { get; set; }
  }
}
