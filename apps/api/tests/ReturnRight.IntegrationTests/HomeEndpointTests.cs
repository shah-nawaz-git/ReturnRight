using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class HomeEndpointTests(ApiFactory factory)
{
    [Fact]
    public async Task Home_counts_and_lists_activity()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);

        await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups",
            new { title = "Chase seller", dueAt = factory.Time.GetUtcNow().AddDays(3) });

        using var response = await client.Http.GetAsync("/api/home");
        response.EnsureSuccessStatusCode();
        var home = await JsonRead.ReadElement(response);

        Assert.Equal(1, home.GetProperty("activeCaseCount").GetInt32());
        Assert.Equal(0, home.GetProperty("overdueFollowUpCount").GetInt32());
        Assert.Equal(0, home.GetProperty("attention").GetArrayLength());

        var upcoming = home.GetProperty("upcomingFollowUps").EnumerateArray().ToArray();
        Assert.Single(upcoming);
        Assert.Equal(caseId, upcoming[0].GetProperty("caseId").GetGuid());
        Assert.Equal("Chase seller", upcoming[0].GetProperty("title").GetString());

        var recent = home.GetProperty("recentCases").EnumerateArray().ToArray();
        Assert.Single(recent);
        Assert.Equal(caseId, recent[0].GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Home_flags_overdue_follow_up_as_attention()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);

        var created = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups",
            new { title = "Soon", dueAt = factory.Time.GetUtcNow().AddMinutes(30) });
        created.EnsureSuccessStatusCode();
        var followUpId = (await JsonRead.ReadElement(created)).GetProperty("id").GetGuid();

        factory.Time.Advance(TimeSpan.FromMinutes(35));

        var home = await JsonRead.ReadElement(await client.Http.GetAsync("/api/home"));
        Assert.Equal(1, home.GetProperty("overdueFollowUpCount").GetInt32());
        var attention = home.GetProperty("attention").EnumerateArray().ToArray();
        Assert.Single(attention);
        Assert.Equal(caseId, attention[0].GetProperty("id").GetGuid());
        Assert.True(attention[0].GetProperty("hasOverdueFollowUp").GetBoolean());

        await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups/{followUpId}/complete", new { });
        var after = await JsonRead.ReadElement(await client.Http.GetAsync("/api/home"));
        Assert.Equal(0, after.GetProperty("overdueFollowUpCount").GetInt32());
        Assert.Equal(0, after.GetProperty("attention").GetArrayLength());
    }

    [Fact]
    public async Task Home_requires_auth()
    {
        using var response = await factory.CreateClient().GetAsync("/api/home");
        Assert.Equal(System.Net.HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
