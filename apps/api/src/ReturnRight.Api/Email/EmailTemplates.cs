using System.Net;

namespace ReturnRight.Api.Email;

public static class EmailTemplates
{
    public static EmailMessage FollowUpReminder(
        string toEmail, string caseTitle, string followUpTitle, DateTimeOffset dueAt, string caseUrl)
    {
        var due = dueAt.ToString("d MMM yyyy");
        var subject = "Follow-up due for your ReturnRight case";

        var text = $"""
            Hi,

            Your follow-up "{followUpTitle}" for the case "{caseTitle}" is due {due}.

            Open your case: {caseUrl}

            —
            You're receiving this because you scheduled a follow-up in ReturnRight.
            """;

        var html = $"""
            <div style="font-family:Arial,sans-serif;max-width:560px;margin:0 auto;color:#101828">
              <h1 style="font-size:18px;color:#315C6B">Follow-up due</h1>
              <p style="font-size:14px;line-height:1.6">
                Your follow-up <strong>{WebUtility.HtmlEncode(followUpTitle)}</strong>
                for the case <strong>{WebUtility.HtmlEncode(caseTitle)}</strong>
                is due <strong>{due}</strong>.
              </p>
              <p style="margin:24px 0">
                <a href="{WebUtility.HtmlEncode(caseUrl)}"
                   style="background:#315C6B;color:#ffffff;padding:10px 20px;border-radius:6px;text-decoration:none;font-size:14px">
                  Open case
                </a>
              </p>
              <p style="font-size:12px;color:#667085;margin-top:32px">
                You're receiving this because you scheduled a follow-up in ReturnRight.
              </p>
            </div>
            """;

        return new EmailMessage(toEmail, subject, text, html);
    }
}
