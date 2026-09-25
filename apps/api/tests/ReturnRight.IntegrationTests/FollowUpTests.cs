using System.Net;
using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class FollowUpTests(ApiFactory factory)
{
    private static async Task<Guid> NewCaseAsync(TestClient client)
    {
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        return await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);
    }

    private static async Task<JsonElement[]> RemindersAsync(TestClient client, Guid caseId)
    {
        using var response = await client.Http.GetAsync($"/api/cases/{caseId}/reminders");
        response.EnsureSuccessStatusCode();
        return (await JsonRead.ReadElement(response)).EnumerateArray().ToArray();
    }

    private static async Task<Guid> CreateFollowUpAsync(
        TestClient client, Guid caseId, string title, DateTimeOffset dueAt)
    {
        using var response = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups", new { title, dueAt });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonRead.ReadElement(response)).GetProperty("id").GetGuid();
    }

    private static string[] TimelineSummaries(JsonElement detail) =>
        detail.GetProperty("timeline").EnumerateArray()
            .Select(e => e.GetProperty("summary").GetString()!)
            .ToArray();

    [Fact]
    public async Task Create_schedules_email_and_in_app_reminders()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        var dueAt = DateTimeOffset.UtcNow.AddDays(2);

        await CreateFollowUpAsync(client, caseId, "Check in", dueAt);

        var reminders = await RemindersAsync(client, caseId);
        Assert.Equal(2, reminders.Length);
        Assert.Equal(
            ["Email", "InApp"],
            reminders.Select(r => r.GetProperty("channel").GetString()!)
                .OrderBy(c => c).ToArray());
        Assert.All(reminders, r =>
        {
            Assert.Equal("Scheduled", r.GetProperty("status").GetString());
            Assert.Equal(dueAt.ToString("O")?[..10],
                r.GetProperty("scheduledFor").GetString()![..10]);
        });

        var detail = await JsonRead.ReadElement(await client.Http.GetAsync($"/api/cases/{caseId}"));
        Assert.Contains(
            TimelineSummaries(detail),
            s => s.StartsWith("Follow-up scheduled for") && s.Contains("Check in"));
    }

    [Fact]
    public async Task Email_preference_off_schedules_only_in_app()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        using var prefs = await client.PatchJsonAsync("/api/profile", new
        {
            emailRemindersEnabled = false,
            inAppRemindersEnabled = true,
        });
        prefs.EnsureSuccessStatusCode();

        var caseId = await NewCaseAsync(client);
        await CreateFollowUpAsync(client, caseId, "Check in", DateTimeOffset.UtcNow.AddDays(1));

        var reminders = await RemindersAsync(client, caseId);
        var reminder = Assert.Single(reminders);
        Assert.Equal("InApp", reminder.GetProperty("channel").GetString());
    }

    [Fact]
    public async Task Patch_due_date_reschedules_reminders()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        var followUpId = await CreateFollowUpAsync(
            client, caseId, "Check in", DateTimeOffset.UtcNow.AddDays(1));

        var newDue = DateTimeOffset.UtcNow.AddDays(5);
        using var patch = await client.PatchJsonAsync(
            $"/api/cases/{caseId}/follow-ups/{followUpId}", new
            {
                title = "Check in",
                dueAt = newDue,
            });
        patch.EnsureSuccessStatusCode();

        var reminders = await RemindersAsync(client, caseId);
        Assert.Equal(4, reminders.Length);
        Assert.Equal(2, reminders.Count(
            r => r.GetProperty("status").GetString() == "Cancelled"));
        Assert.Equal(2, reminders.Count(
            r => r.GetProperty("status").GetString() == "Scheduled"));

        var detail = await JsonRead.ReadElement(await client.Http.GetAsync($"/api/cases/{caseId}"));
        Assert.Contains(
            detail.GetProperty("timeline").EnumerateArray(),
            e => e.GetProperty("eventType").GetString() == "FollowUpRescheduled");
    }

    [Fact]
    public async Task Complete_cancels_reminders_and_writes_timeline()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        var followUpId = await CreateFollowUpAsync(
            client, caseId, "Check in", DateTimeOffset.UtcNow.AddDays(1));

        using var complete = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups/{followUpId}/complete", new { });
        complete.EnsureSuccessStatusCode();

        var reminders = await RemindersAsync(client, caseId);
        Assert.All(reminders,
            r => Assert.Equal("Cancelled", r.GetProperty("status").GetString()));

        var detail = await JsonRead.ReadElement(await client.Http.GetAsync($"/api/cases/{caseId}"));
        Assert.Contains(TimelineSummaries(detail), s => s == "Follow-up done: Check in");
    }

    [Fact]
    public async Task Cancel_cancels_reminders_and_writes_timeline()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        var followUpId = await CreateFollowUpAsync(
            client, caseId, "Check in", DateTimeOffset.UtcNow.AddDays(1));

        using var cancel = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups/{followUpId}/cancel", new { });
        cancel.EnsureSuccessStatusCode();

        var reminders = await RemindersAsync(client, caseId);
        Assert.All(reminders,
            r => Assert.Equal("Cancelled", r.GetProperty("status").GetString()));

        var detail = await JsonRead.ReadElement(await client.Http.GetAsync($"/api/cases/{caseId}"));
        Assert.Contains(TimelineSummaries(detail), s => s == "Follow-up cancelled: Check in");
    }

    [Fact]
    public async Task Resolving_case_cancels_scheduled_reminders()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        await CreateFollowUpAsync(
            client, caseId, "Check in", DateTimeOffset.UtcNow.AddDays(1));

        using var resolve = await client.PostJsonAsync($"/api/cases/{caseId}/resolve", new
        {
            finalOutcomeType = "FullRefundReceived",
        });
        resolve.EnsureSuccessStatusCode();

        var reminders = await RemindersAsync(client, caseId);
        Assert.All(reminders,
            r => Assert.Equal("Cancelled", r.GetProperty("status").GetString()));
    }

    [Fact]
    public async Task Past_due_date_is_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        using var response = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups", new
            {
                title = "Too late",
                dueAt = DateTimeOffset.UtcNow.AddDays(-1),
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
