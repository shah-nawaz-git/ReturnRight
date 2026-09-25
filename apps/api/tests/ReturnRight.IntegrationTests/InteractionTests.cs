using System.Net;
using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class InteractionTests(ApiFactory factory)
{
    private static async Task<Guid> NewCaseAsync(TestClient client)
    {
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        return await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);
    }

    private static async Task<JsonElement> DetailAsync(TestClient client, Guid caseId)
    {
        using var response = await client.Http.GetAsync($"/api/cases/{caseId}");
        response.EnsureSuccessStatusCode();
        return await JsonRead.ReadElement(response);
    }

    private static string[] TimelineTypes(JsonElement detail) =>
        detail.GetProperty("timeline").EnumerateArray()
            .Select(e => e.GetProperty("eventType").GetString()!)
            .ToArray();

    private static async Task<Guid> AddInteractionAsync(
        TestClient client, Guid caseId, object body)
    {
        using var response = await client.PostJsonAsync(
            $"/api/cases/{caseId}/interactions", body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await JsonRead.ReadElement(response)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Contacted_seller_moves_status_and_writes_two_events()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        await AddInteractionAsync(client, caseId, new
        {
            interactionType = "ContactedSeller",
            occurredAt = DateTimeOffset.UtcNow,
            note = "Emailed the seller with photos.",
        });

        var detail = await DetailAsync(client, caseId);
        Assert.Equal("SellerContacted", detail.GetProperty("status").GetString());
        var types = TimelineTypes(detail);
        Assert.Contains("InteractionAdded", types);
        Assert.Contains("StatusChanged", types);

        var interaction = detail.GetProperty("interactions").EnumerateArray().Single();
        Assert.Equal("ContactedSeller", interaction.GetProperty("interactionType").GetString());
    }

    [Fact]
    public async Task Refund_promised_sets_pending_and_outcome_fields()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        await AddInteractionAsync(client, caseId, new
        {
            interactionType = "RefundPromised",
            occurredAt = DateTimeOffset.UtcNow,
            note = "Seller agreed to refund.",
            expectedBy = "2026-11-15",
        });

        var detail = await DetailAsync(client, caseId);
        Assert.Equal("RefundPending", detail.GetProperty("status").GetString());
        Assert.Equal("2026-11-15", detail.GetProperty("outcomeExpectedBy").GetString());
        Assert.NotEqual(JsonValueKind.Null,
            detail.GetProperty("outcomePromisedAt").ValueKind);
        Assert.Contains(
            detail.GetProperty("timeline").EnumerateArray(),
            e => e.GetProperty("eventType").GetString() == "OutcomePromised"
                && e.GetProperty("summary").GetString()!
                    .Contains("Seller promised a refund"));
    }

    [Fact]
    public async Task Future_occurred_at_is_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        using var response = await client.PostJsonAsync(
            $"/api/cases/{caseId}/interactions", new
            {
                interactionType = "ContactedSeller",
                occurredAt = DateTimeOffset.UtcNow.AddHours(1),
                note = "Time traveller",
            });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Attachment_upload_and_delete()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        var interactionId = await AddInteractionAsync(client, caseId, new
        {
            interactionType = "ContactedSeller",
            occurredAt = DateTimeOffset.UtcNow,
            note = "Called them.",
        });

        using var upload = await client.PostFileAsync(
            $"/api/cases/{caseId}/interactions/{interactionId}/attachment",
            TestFiles.Png, "call-notes.png", "image/png");
        upload.EnsureSuccessStatusCode();
        var body = await JsonRead.ReadElement(upload);
        Assert.Equal("call-notes.png",
            body.GetProperty("document").GetProperty("fileName").GetString());

        var before = factory.StoredFileCount();
        using var delete = await client.DeleteAsync(
            $"/api/cases/{caseId}/interactions/{interactionId}/attachment");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(before - 1, factory.StoredFileCount());

        var detail = await DetailAsync(client, caseId);
        var interaction = detail.GetProperty("interactions").EnumerateArray().Single();
        Assert.Equal(JsonValueKind.Null, interaction.GetProperty("document").ValueKind);
    }

    [Fact]
    public async Task Patch_edits_without_reapplying_side_effects()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        var interactionId = await AddInteractionAsync(client, caseId, new
        {
            interactionType = "ContactedSeller",
            occurredAt = DateTimeOffset.UtcNow,
            note = "First note",
        });

        using var patch = await client.PatchJsonAsync(
            $"/api/cases/{caseId}/interactions/{interactionId}", new
            {
                interactionType = "RefundPromised",
                occurredAt = DateTimeOffset.UtcNow,
                note = "Corrected note",
            });
        patch.EnsureSuccessStatusCode();
        var body = await JsonRead.ReadElement(patch);
        Assert.Equal("RefundPromised", body.GetProperty("interactionType").GetString());
        Assert.Equal("Corrected note", body.GetProperty("note").GetString());

        // Patching must not re-run the promise side effects.
        var detail = await DetailAsync(client, caseId);
        Assert.Equal(JsonValueKind.Null,
            detail.GetProperty("outcomePromisedAt").ValueKind);
        Assert.Equal("SellerContacted", detail.GetProperty("status").GetString());
    }

    [Fact]
    public async Task Delete_interaction()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        var interactionId = await AddInteractionAsync(client, caseId, new
        {
            interactionType = "ContactedSeller",
            occurredAt = DateTimeOffset.UtcNow,
            note = "First note",
        });

        using var delete = await client.DeleteAsync(
            $"/api/cases/{caseId}/interactions/{interactionId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);

        var detail = await DetailAsync(client, caseId);
        Assert.Empty(detail.GetProperty("interactions").EnumerateArray());
    }

    [Fact]
    public async Task Resolved_case_rejects_interaction_changes()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);
        var interactionId = await AddInteractionAsync(client, caseId, new
        {
            interactionType = "ContactedSeller",
            occurredAt = DateTimeOffset.UtcNow,
            note = "note",
        });

        using var resolve = await client.PostJsonAsync($"/api/cases/{caseId}/resolve", new
        {
            finalOutcomeType = "FullRefundReceived",
        });
        resolve.EnsureSuccessStatusCode();

        using var add = await client.PostJsonAsync(
            $"/api/cases/{caseId}/interactions", new
            {
                interactionType = "SellerResponded",
                occurredAt = DateTimeOffset.UtcNow,
                note = "Late reply",
            });
        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);

        using var patch = await client.PatchJsonAsync(
            $"/api/cases/{caseId}/interactions/{interactionId}", new
            {
                interactionType = "SellerResponded",
                occurredAt = DateTimeOffset.UtcNow,
                note = "edit",
            });
        Assert.Equal(HttpStatusCode.Conflict, patch.StatusCode);

        using var delete = await client.DeleteAsync(
            $"/api/cases/{caseId}/interactions/{interactionId}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }
}
