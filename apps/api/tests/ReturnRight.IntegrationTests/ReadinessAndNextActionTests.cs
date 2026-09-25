using System.Net;
using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class ReadinessAndNextActionTests(ApiFactory factory)
{
    private static async Task<JsonElement> DetailAsync(TestClient client, Guid caseId)
    {
        using var response = await client.Http.GetAsync($"/api/cases/{caseId}");
        response.EnsureSuccessStatusCode();
        return await JsonRead.ReadElement(response);
    }

    private static async Task<Guid> PreparedCaseAsync(TestClient client)
    {
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        using var doc = await client.PostFileAsync(
            $"/api/purchases/{purchaseId}/documents",
            TestFiles.Pdf, "receipt.pdf", "application/pdf");
        doc.EnsureSuccessStatusCode();
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);
        return caseId;
    }

    private static async Task AddInteractionAsync(TestClient client, Guid caseId)
    {
        using var response = await client.PostJsonAsync(
            $"/api/cases/{caseId}/interactions", new
            {
                interactionType = "ContactedSeller",
                occurredAt = DateTimeOffset.UtcNow,
                note = "Emailed the seller.",
            });
        response.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task Fresh_case_with_proof_suggests_contact_seller()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await PreparedCaseAsync(client);

        var detail = await DetailAsync(client, caseId);
        Assert.Equal("contact_seller", detail.GetProperty("nextAction").GetProperty("key").GetString());

        using var readiness = await client.Http.GetAsync($"/api/cases/{caseId}/readiness");
        readiness.EnsureSuccessStatusCode();
        var body = await JsonRead.ReadElement(readiness);
        var proof = body.GetProperty("items").EnumerateArray()
            .Single(i => i.GetProperty("key").GetString() == "purchase_proof");
        Assert.True(proof.GetProperty("isComplete").GetBoolean());
    }

    [Fact]
    public async Task After_interaction_next_action_moves_on()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await PreparedCaseAsync(client);
        await AddInteractionAsync(client, caseId);

        var detail = await DetailAsync(client, caseId);
        var key = detail.GetProperty("nextAction").GetProperty("key").GetString()!;
        Assert.True(
            key == "schedule_followup" || key.StartsWith("readiness:"),
            $"expected schedule_followup or a readiness item, got {key}");
    }

    [Fact]
    public async Task Overdue_follow_up_becomes_the_next_action()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        factory.Time.SetUtcNow(DateTimeOffset.UtcNow);
        var caseId = await PreparedCaseAsync(client);
        await AddInteractionAsync(client, caseId);

        using var create = await client.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups", new
            {
                title = "Check in",
                dueAt = factory.Time.GetUtcNow().AddMinutes(30),
            });
        create.EnsureSuccessStatusCode();
        var followUpId = (await JsonRead.ReadElement(create)).GetProperty("id").GetGuid();

        factory.Time.Advance(TimeSpan.FromHours(1));

        var detail = await DetailAsync(client, caseId);
        var next = detail.GetProperty("nextAction");
        Assert.Equal($"followup_overdue:{followUpId}",
            next.GetProperty("key").GetString());
        Assert.Equal(followUpId.ToString(),
            next.GetProperty("followUpId").GetGuid().ToString());
    }

    [Fact]
    public async Task Resolved_case_next_action_is_resolved()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await PreparedCaseAsync(client);
        using var resolve = await client.PostJsonAsync($"/api/cases/{caseId}/resolve", new
        {
            finalOutcomeType = "FullRefundReceived",
        });
        resolve.EnsureSuccessStatusCode();

        var detail = await DetailAsync(client, caseId);
        Assert.Equal("resolved", detail.GetProperty("nextAction").GetProperty("key").GetString());
    }

    [Fact]
    public async Task Dismiss_skips_suggestion_and_new_activity_restores_it()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await PreparedCaseAsync(client);
        await AddInteractionAsync(client, caseId);

        // Fresh case description is short → readiness:problem_description is next.
        var detail = await DetailAsync(client, caseId);
        var key = detail.GetProperty("nextAction").GetProperty("key").GetString()!;
        Assert.True(key.StartsWith("readiness:") || key == "schedule_followup");
        Assert.True(detail.GetProperty("nextAction").GetProperty("isDismissible").GetBoolean());

        using var dismiss = await client.PostJsonAsync(
            $"/api/cases/{caseId}/next-action/dismiss", new { key });
        dismiss.EnsureSuccessStatusCode();
        var dismissed = await JsonRead.ReadElement(dismiss);
        Assert.NotEqual(key, dismissed.GetProperty("key").GetString());

        // A new timeline event clears the dismissal.
        using var second = await client.PostJsonAsync(
            $"/api/cases/{caseId}/interactions", new
            {
                interactionType = "SellerResponded",
                occurredAt = DateTimeOffset.UtcNow,
                note = "Seller answered.",
            });
        second.EnsureSuccessStatusCode();

        detail = await DetailAsync(client, caseId);
        Assert.Equal(key, detail.GetProperty("nextAction").GetProperty("key").GetString());
    }

    [Fact]
    public async Task Dismissing_a_non_dismissible_key_is_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await PreparedCaseAsync(client);

        using var dismiss = await client.PostJsonAsync(
            $"/api/cases/{caseId}/next-action/dismiss", new { key = "contact_seller" });
        Assert.Equal(HttpStatusCode.BadRequest, dismiss.StatusCode);
    }
}
