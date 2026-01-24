using System.Net;
using System.Net.Mail;
using BetsTrading.Application.Interfaces;

namespace BetsTrading.Infrastructure.Services;

public class SmtpSettings
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 587;
    public bool EnableSsl { get; set; } = true;
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string FromName { get; set; } = "Betrader Support";
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
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(_settings.Host)) missing.Add("Host (SMTP__Host o appsettings SMTP:Host)");
        if (string.IsNullOrWhiteSpace(_settings.Username)) missing.Add("Username (SMTP__Username o appsettings SMTP:Username)");
        if (string.IsNullOrWhiteSpace(_settings.FromAddress)) missing.Add("FromAddress (SMTP__FromAddress o appsettings SMTP:FromAddress)");
        if (string.IsNullOrWhiteSpace(_settings.Password)) missing.Add("Password (SMTP__Password en .env)");
        if (missing.Count > 0)
            throw new InvalidOperationException(
                "SMTP no configurado. Faltan: " + string.Join(", ", missing) + ".");

        // Gmail requiere TLS 1.2+; forzar por si el runtime usa protocolo por defecto antigua
        ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

        using var client = new SmtpClient(_settings.Host!, _settings.Port)
        {
            Credentials = new NetworkCredential(_settings.Username, _settings.Password),
            EnableSsl = _settings.EnableSsl
        };

        var mail = new MailMessage
        {
            From = new MailAddress(_settings.FromAddress!, _settings.FromName),
            Subject = subject,
            Body = body,
            IsBodyHtml = false
        };

        mail.To.Add(to);

        await client.SendMailAsync(mail);
    }
}
