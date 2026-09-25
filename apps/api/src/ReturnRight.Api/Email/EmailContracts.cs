namespace ReturnRight.Api.Email;

public record EmailMessage(
    string ToEmail,
    string Subject,
    string TextBody,
    string HtmlBody);

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken ct);
}
