namespace BetsTrading_Service.Services
{
  using System.Net;
  using System.Net.Mail;
  using System.Threading.Tasks;

  public interface IEmailService
  {
    Task SendEmailAsync(string to, string subject, string body);
  }

  public class EmailService : IEmailService
  {
    private readonly SmtpSettings _settings;

    public EmailService(SmtpSettings settings)
    {
      _settings = settings;
    }

    public async Task SendEmailAsync(string to, string subject, string body)
    {
      using var client = new SmtpClient(_settings.Host, _settings.Port)
      {
        Credentials = new NetworkCredential(_settings.Username, _settings.Password),
        EnableSsl = _settings.EnableSsl
      };

      var mail = new MailMessage
      {
        From = new MailAddress(_settings.FromAddress, _settings.FromName),
        Subject = subject,
        Body = body,
        IsBodyHtml = false
      };

      mail.To.Add(to);

      await client.SendMailAsync(mail);
    }
  }

  public class SmtpSettings
  {
    public string Host { get; set; } = "";
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = "";
    public string Password { get; set; } = "";
    public string FromAddress { get; set; } = "";
    public string FromName { get; set; } = "Betrader Support";
  }


}
