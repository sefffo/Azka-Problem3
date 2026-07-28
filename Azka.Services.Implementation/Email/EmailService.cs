using Azka.Services.Interfaces;
using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace Azka.Services.Implementation.Email;

/// <summary>
/// Sends transactional emails via SMTP (Gmail / any provider) using MailKit.
/// Uses STARTTLS on port 587 — more explicit and safer than the old bool UseSsl overload.
/// </summary>
public class EmailService(
    IOptions<EmailSettings> emailSettings,
    ILogger<EmailService> logger
) : IEmailService
{
    public async Task SendAsync(string to, string subject, string body, bool isHtml = true)
    {
        var settings = emailSettings.Value;
        logger.LogInformation("[Email] Preparing '{Subject}' → {To} via {Host}:{Port}",
            subject, to, settings.Host, settings.Port);

        var message = new MimeMessage();
        message.From.Add(new MailboxAddress(settings.FromName, settings.FromEmail));
        message.To.Add(new MailboxAddress(to, to));
        message.Subject = subject;
        message.Body = new TextPart(isHtml ? "html" : "plain") { Text = body };

        using var client = new SmtpClient();
        try
        {
            await client.ConnectAsync(settings.Host, settings.Port, SecureSocketOptions.StartTls);
            logger.LogInformation("[Email] Connected. Authenticating as {Username}...", settings.Username);

            await client.AuthenticateAsync(settings.Username, settings.Password);
            logger.LogInformation("[Email] Authenticated. Sending to {To}...", to);

            await client.SendAsync(message);
            logger.LogInformation("[Email] Sent successfully to {To}.", to);
        }
        catch (AuthenticationException authEx)
        {
            logger.LogError(authEx,
                "[Email] AUTH FAILED for {Username}. Check Gmail App Password & 2FA.",
                settings.Username);
            throw;
        }
        catch (SmtpCommandException smtpEx)
        {
            logger.LogError(smtpEx,
                "[Email] SMTP ERROR — Status: {StatusCode}, Msg: {SmtpMessage}",
                smtpEx.StatusCode, smtpEx.Message);
            throw;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "[Email] Unexpected error sending to {To}.", to);
            throw;
        }
        finally
        {
            if (client.IsConnected)
                await client.DisconnectAsync(true);
        }
    }
}
