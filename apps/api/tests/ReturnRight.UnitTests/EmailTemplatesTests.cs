using ReturnRight.Api.Email;

namespace ReturnRight.UnitTests;

public class EmailTemplatesTests
{
    [Fact]
    public void Follow_up_email_has_fixed_subject_and_contains_case_url()
    {
        var message = EmailTemplates.FollowUpReminder(
            "user@test.local",
            "Broken headphones from SoundMarket",
            "Check in with the seller",
            new DateTimeOffset(2026, 10, 5, 9, 0, 0, TimeSpan.Zero),
            "http://localhost:3000/cases/abc123");

        Assert.Equal("Follow-up due for your ReturnRight case", message.Subject);
        Assert.Contains("http://localhost:3000/cases/abc123", message.HtmlBody);
        Assert.Contains("http://localhost:3000/cases/abc123", message.TextBody);
        Assert.Contains("5 Oct 2026", message.HtmlBody);
    }

    [Fact]
    public void User_controlled_strings_are_html_encoded()
    {
        var message = EmailTemplates.FollowUpReminder(
            "user@test.local",
            "Case <script>alert(1)</script>",
            "Follow <script>alert(2)</script>",
            DateTimeOffset.UtcNow,
            "http://localhost:3000/cases/abc123");

        Assert.DoesNotContain("<script>", message.HtmlBody);
        Assert.Contains("&lt;script&gt;", message.HtmlBody);
    }
}
