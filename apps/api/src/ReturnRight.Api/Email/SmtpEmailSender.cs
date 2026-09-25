using MailKit.Net.Smtp;
using MailKit.Security;
using MimeKit;

namespace ReturnRight.Api.Email;

public class SmtpEmailSender(IConfiguration configuration, ILogger<SmtpEmailSender> logger)
    : IEmailSender
{
    public async Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        var host = configuration["Smtp:Host"] ?? "localhost";
        var port = configuration.GetValue("Smtp:Port", 1025);
        var from = configuration["Smtp:From"] ?? "ReturnRight <no-reply@returnright.local>";
        var username = configuration["Smtp:Username"];
        var password = configuration["Smtp:Password"];
        var useStartTls = configuration.GetValue("Smtp:UseStartTls", false);

        var mime = new MimeMessage();
        mime.From.Add(MailboxAddress.Parse(from));
        mime.To.Add(MailboxAddress.Parse(message.ToEmail));
        mime.Subject = message.Subject;
        mime.Body = new BodyBuilder
        {
            TextBody = message.TextBody,
            HtmlBody = message.HtmlBody,
        }.ToMessageBody();

        using var client = new SmtpClient();
        var secure = useStartTls ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
        await client.ConnectAsync(host, port, secure, ct);
        if (!string.IsNullOrEmpty(username))
        {
            await client.AuthenticateAsync(username, password ?? string.Empty, ct);
        }
        await client.SendAsync(mime, ct);
        await client.DisconnectAsync(quit: true, ct);
        logger.LogInformation("Sent reminder email ({Subject})", message.Subject);
    }
}
