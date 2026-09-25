using System.Net;
using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class NotificationTests(ApiFactory factory)
{
    /// <summary>Produces one in-app notification via the reminder pipeline.</summary>
    private async Task<TestClient> UserWithNotificationAsync()
    {
        factory.Time.SetUtcNow(DateTimeOffset.UtcNow);
        factory.EmailSender.Reset();

        var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);

        using var create = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups", new
            {
                title = "Check in",
                dueAt = factory.Time.GetUtcNow().AddMinutes(1),
            });
        create.EnsureSuccessStatusCode();

        factory.Time.Advance(TimeSpan.FromMinutes(2));
        using var process = await client.PostJsonAsync("/api/dev/reminders/process", new { });
        process.EnsureSuccessStatusCode();
        return client;
    }

    private static async Task<JsonElement[]> ListAsync(TestClient client)
    {
        using var response = await client.Http.GetAsync("/api/notifications");
        response.EnsureSuccessStatusCode();
        return (await JsonRead.ReadElement(response)).EnumerateArray().ToArray();
    }

    [Fact]
    public async Task List_read_read_all_and_delete()
    {
        var client = await UserWithNotificationAsync();
        using var _ = client;

        var notifications = await ListAsync(client);
        var notification = Assert.Single(notifications);
        Assert.Equal(JsonValueKind.Null, notification.GetProperty("readAt").ValueKind);
        var id = notification.GetProperty("id").GetGuid();

        using var read = await client.PostJsonAsync($"/api/notifications/{id}/read", new { });
        read.EnsureSuccessStatusCode();
        var after = await ListAsync(client);
        Assert.NotEqual(JsonValueKind.Null,
            after[0].GetProperty("readAt").ValueKind);

        // Second notification → read-all clears everything.
        factory.Time.Advance(TimeSpan.FromMinutes(2));
        var caseId = notification.GetProperty("caseId").GetGuid();
        using var create = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups", new
            {
                title = "Second",
                dueAt = factory.Time.GetUtcNow().AddMinutes(1),
            });
        create.EnsureSuccessStatusCode();
        factory.Time.Advance(TimeSpan.FromMinutes(2));
        using var process = await client.PostJsonAsync("/api/dev/reminders/process", new { });
        process.EnsureSuccessStatusCode();

        using var readAll = await client.PostJsonAsync("/api/notifications/read-all", new { });
        readAll.EnsureSuccessStatusCode();
        Assert.All(await ListAsync(client),
            n => Assert.NotEqual(JsonValueKind.Null, n.GetProperty("readAt").ValueKind));

        var firstId = (await ListAsync(client))[0].GetProperty("id").GetGuid();
        using var delete = await client.DeleteAsync($"/api/notifications/{firstId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.DoesNotContain(await ListAsync(client),
            n => n.GetProperty("id").GetGuid() == firstId);
    }

    [Fact]
    public async Task Foreign_notification_returns_404()
    {
        var client = await UserWithNotificationAsync();
        using var _ = client;
        using var other = await TestClient.RegisterNewUserAsync(factory);

        var id = (await ListAsync(client))[0].GetProperty("id").GetGuid();

        using var read = await other.PostJsonAsync($"/api/notifications/{id}/read", new { });
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);

        using var delete = await other.DeleteAsync($"/api/notifications/{id}");
        Assert.Equal(HttpStatusCode.NotFound, delete.StatusCode);
    }

    [Fact]
    public async Task Anonymous_requests_are_rejected()
    {
        using var anonymous = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

        using var list = await anonymous.GetAsync("/api/notifications");
        Assert.Equal(HttpStatusCode.Unauthorized, list.StatusCode);
    }
}
