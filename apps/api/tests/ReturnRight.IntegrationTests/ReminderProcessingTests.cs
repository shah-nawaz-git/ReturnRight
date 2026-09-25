using System.Net;
using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class ReminderProcessingTests(ApiFactory factory)
{
    private async Task<(TestClient Client, Guid CaseId)> SetupDueFollowUpAsync(
        string title = "Check in with the seller", bool emailEnabled = true)
    {
        factory.Time.SetUtcNow(DateTimeOffset.UtcNow);
        factory.EmailSender.Reset();

        var client = await TestClient.RegisterNewUserAsync(factory);
        if (!emailEnabled)
        {
            using var prefs = await client.PatchJsonAsync("/api/profile", new
            {
                emailRemindersEnabled = false,
                inAppRemindersEnabled = true,
            });
            prefs.EnsureSuccessStatusCode();
        }

        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);

        using var create = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups", new
            {
                title,
                dueAt = factory.Time.GetUtcNow().AddMinutes(1),
            });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);

        factory.Time.Advance(TimeSpan.FromMinutes(2)); // follow-up now due
        return (client, caseId);
    }

    private static async Task<JsonElement[]> RemindersAsync(TestClient client, Guid caseId)
    {
        using var response = await client.Http.GetAsync($"/api/cases/{caseId}/reminders");
        response.EnsureSuccessStatusCode();
        return (await JsonRead.ReadElement(response)).EnumerateArray().ToArray();
    }

    private static async Task<JsonElement> ProcessAsync(TestClient client)
    {
        using var response = await client.PostJsonAsync("/api/dev/reminders/process", new { });
        response.EnsureSuccessStatusCode();
        return await JsonRead.ReadElement(response);
    }

    [Fact]
    public async Task Due_email_reminder_is_sent_and_recorded()
    {
        var (client, caseId) = await SetupDueFollowUpAsync();
        using var _ = client;

        var result = await ProcessAsync(client);
        Assert.Equal(2, result.GetProperty("processed").GetInt32());
        Assert.Equal(2, result.GetProperty("sent").GetInt32());

        var sent = Assert.Single(factory.EmailSender.Sent);
        Assert.Equal("Follow-up due for your ReturnRight case", sent.Subject);
        Assert.Equal(client.Email, sent.ToEmail);
        Assert.Contains($"/cases/{caseId}", sent.HtmlBody);
        Assert.Contains("Check in with the seller", sent.HtmlBody);

        var reminders = await RemindersAsync(client, caseId);
        Assert.All(reminders, r =>
        {
            Assert.Equal("Sent", r.GetProperty("status").GetString());
            Assert.NotEqual(JsonValueKind.Null, r.GetProperty("sentAt").ValueKind);
        });
    }

    [Fact]
    public async Task Failed_send_retries_after_five_minutes()
    {
        var (client, caseId) = await SetupDueFollowUpAsync();
        using var _ = client;
        factory.EmailSender.FailuresRemaining = 1;

        var before = factory.Time.GetUtcNow();
        await ProcessAsync(client);

        var email = (await RemindersAsync(client, caseId))
            .Single(r => r.GetProperty("channel").GetString() == "Email");
        Assert.Equal("Scheduled", email.GetProperty("status").GetString());
        Assert.Equal(1, email.GetProperty("attemptCount").GetInt32());
        var nextAttempt = email.GetProperty("nextAttemptAt").GetDateTimeOffset();
        Assert.Equal(before.AddMinutes(5), nextAttempt, TimeSpan.FromSeconds(1));
        Assert.NotEqual(JsonValueKind.Null, email.GetProperty("lastError").ValueKind);
        Assert.DoesNotContain(client.Email, email.GetProperty("lastError").GetString());
    }

    [Fact]
    public async Task Four_failures_mark_reminder_failed_and_notify()
    {
        var (client, caseId) = await SetupDueFollowUpAsync();
        using var _ = client;
        factory.EmailSender.FailuresRemaining = 10;

        foreach (var advance in new[] { 0, 5, 30, 120 })
        {
            if (advance > 0)
            {
                factory.Time.Advance(TimeSpan.FromMinutes(advance));
            }
            await ProcessAsync(client);
        }

        var email = (await RemindersAsync(client, caseId))
            .Single(r => r.GetProperty("channel").GetString() == "Email");
        Assert.True(email.GetProperty("status").GetString() == "Failed", email.ToString());
        Assert.Equal(4, email.GetProperty("attemptCount").GetInt32());

        using var list = await client.Http.GetAsync("/api/notifications");
        var notifications = await JsonRead.ReadElement(list);
        Assert.Contains(
            notifications.EnumerateArray(),
            n => n.GetProperty("title").GetString()!
                .Contains("couldn't send your reminder email"));
    }

    [Fact]
    public async Task Completed_follow_up_cancels_pending_reminder()
    {
        var (client, caseId) = await SetupDueFollowUpAsync();
        using var _ = client;

        var followUpId = (await JsonRead.ReadElement(
                await client.Http.GetAsync($"/api/cases/{caseId}")))
            .GetProperty("followUps").EnumerateArray().Single()
            .GetProperty("id").GetGuid();
        using var complete = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups/{followUpId}/complete", new { });
        complete.EnsureSuccessStatusCode();

        await ProcessAsync(client);
        Assert.Empty(factory.EmailSender.Sent);

        var reminders = await RemindersAsync(client, caseId);
        Assert.All(reminders,
            r => Assert.Equal("Cancelled", r.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task In_app_reminder_creates_notification_and_unread_count()
    {
        var (client, caseId) = await SetupDueFollowUpAsync(emailEnabled: false);
        using var _ = client;

        using var meBefore = await client.Http.GetAsync("/api/auth/me");
        var unreadBefore = (await JsonRead.ReadElement(meBefore))
            .GetProperty("unreadNotifications").GetInt32();

        var result = await ProcessAsync(client);
        Assert.Equal(1, result.GetProperty("sent").GetInt32());

        using var meAfter = await client.Http.GetAsync("/api/auth/me");
        var unreadAfter = (await JsonRead.ReadElement(meAfter))
            .GetProperty("unreadNotifications").GetInt32();
        Assert.Equal(unreadBefore + 1, unreadAfter);

        using var list = await client.Http.GetAsync("/api/notifications");
        var notifications = await JsonRead.ReadElement(list);
        Assert.Contains(
            notifications.EnumerateArray(),
            n => n.GetProperty("title").GetString()!
                .Contains("Check in with the seller"));
    }

    [Fact]
    public async Task Process_endpoint_requires_authentication()
    {
        using var anonymous = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });
        anonymous.DefaultRequestHeaders.Add("X-CSRF-TOKEN", "anything");

        using var response = await anonymous.PostAsync(
            "/api/dev/reminders/process", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
