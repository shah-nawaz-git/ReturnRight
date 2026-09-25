using System.Net;
using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class CaseTests(ApiFactory factory)
{
    private static string[] TimelineTypes(JsonElement detail) =>
        detail.GetProperty("timeline").EnumerateArray()
            .Select(e => e.GetProperty("eventType").GetString()!)
            .ToArray();

    private static string[] TimelineSummaries(JsonElement detail) =>
        detail.GetProperty("timeline").EnumerateArray()
            .Select(e => e.GetProperty("summary").GetString()!)
            .ToArray();

    private async Task<(Guid CaseId, JsonElement Detail)> DetailAsync(
        TestClient client, Guid caseId)
    {
        using var response = await client.Http.GetAsync($"/api/cases/{caseId}");
        response.EnsureSuccessStatusCode();
        var body = await JsonRead.ReadElement(response);
        return (caseId, body);
    }

    [Fact]
    public async Task Create_with_purchase_and_items_adds_proof_and_timeline()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        using var upload = await client.PostFileAsync(
            $"/api/purchases/{purchaseId}/documents",
            TestFiles.Pdf, "receipt.pdf", "application/pdf");
        upload.EnsureSuccessStatusCode();

        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);
        var (_, detail) = await DetailAsync(client, caseId);

        Assert.Equal("Item 1 and 1 more", detail.GetProperty("title").GetString());
        Assert.Equal("Open", detail.GetProperty("status").GetString());
        Assert.Equal("SoundMarket",
            detail.GetProperty("purchase").GetProperty("merchantName").GetString());

        var evidence = detail.GetProperty("evidence").EnumerateArray().ToArray();
        Assert.Single(evidence);
        Assert.Equal("PurchaseProof", evidence[0].GetProperty("evidenceType").GetString());
        Assert.Equal("receipt.pdf",
            evidence[0].GetProperty("document").GetProperty("fileName").GetString());

        var types = TimelineTypes(detail);
        Assert.Contains("CaseCreated", types);
        Assert.Contains("PurchaseLinked", types);
        Assert.Contains("EvidenceAdded", types);
        Assert.Contains(
            TimelineSummaries(detail), s => s == "Purchase proof added: receipt.pdf");
    }

    [Fact]
    public async Task Items_outside_purchase_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, _) = await PurchaseTests.CreatePurchaseAsync(client);
        var (_, otherItemIds) = await PurchaseTests.CreatePurchaseAsync(client);

        using var response = await client.PostJsonAsync("/api/cases", new
        {
            problemType = "DamagedItem",
            description = "Arrived broken.",
            requestedOutcomeType = "FullRefund",
            purchaseId,
            affectedItemIds = otherItemIds,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Contains("don't belong", body.ToString());
    }

    [Fact]
    public async Task Patch_requested_outcome_writes_timeline()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await PurchaseTests.CreateCaseAsync(
            client, (await PurchaseTests.CreatePurchaseAsync(client)).PurchaseId, []);

        using var response = await client.PatchJsonAsync($"/api/cases/{caseId}", new
        {
            problemType = "DamagedItem",
            description = "Arrived broken.",
            requestedOutcomeType = "Replacement",
            requestedAmount = 389.99m,
            requestedCurrency = "EUR",
        });
        response.EnsureSuccessStatusCode();

        var (_, detail) = await DetailAsync(client, caseId);
        Assert.Equal("Replacement", detail.GetProperty("requestedOutcomeType").GetString());
        Assert.Contains(
            TimelineSummaries(detail),
            s => s == "Requested outcome changed to Replacement");
    }

    [Fact]
    public async Task Status_change_and_resolved_rejected_via_status()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, _) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, []);

        using var status = await client.PostJsonAsync($"/api/cases/{caseId}/status", new
        {
            status = "SellerContacted",
            note = "Sent them a message",
        });
        status.EnsureSuccessStatusCode();

        var (_, detail) = await DetailAsync(client, caseId);
        Assert.Equal("SellerContacted", detail.GetProperty("status").GetString());
        Assert.Contains(
            TimelineSummaries(detail),
            s => s == "Status changed to Seller contacted — Sent them a message");

        using var badStatus = await client.PostJsonAsync($"/api/cases/{caseId}/status", new
        {
            status = "Resolved",
        });
        Assert.Equal(HttpStatusCode.BadRequest, badStatus.StatusCode);
        var body = await JsonRead.ReadElement(badStatus);
        Assert.Equal("Use Resolve to finish a case.", JsonRead.ProblemTitle(body));

        using var sameStatus = await client.PostJsonAsync($"/api/cases/{caseId}/status", new
        {
            status = "SellerContacted",
        });
        Assert.Equal(HttpStatusCode.BadRequest, sameStatus.StatusCode);
    }

    [Fact]
    public async Task Resolve_success_sets_resolved_and_final_outcome()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, _) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, []);

        using var response = await client.PostJsonAsync($"/api/cases/{caseId}/resolve", new
        {
            finalOutcomeType = "FullRefundReceived",
            finalAmount = 389.99m,
            finalCurrency = "EUR",
        });
        response.EnsureSuccessStatusCode();

        var (_, detail) = await DetailAsync(client, caseId);
        Assert.Equal("Resolved", detail.GetProperty("status").GetString());
        var final = detail.GetProperty("finalOutcome");
        Assert.Equal("FullRefundReceived", final.GetProperty("type").GetString());
        Assert.Equal(389.99m, final.GetProperty("amount").GetDecimal());
        Assert.NotEqual(JsonValueKind.Null, detail.GetProperty("resolvedAt").ValueKind);
        Assert.Contains(
            TimelineSummaries(detail), s => s == "Case resolved: Full refund received");

        using var again = await client.PostJsonAsync($"/api/cases/{caseId}/resolve", new
        {
            finalOutcomeType = "FullRefundReceived",
        });
        Assert.Equal(HttpStatusCode.BadRequest, again.StatusCode);
    }

    [Fact]
    public async Task Rejected_outcome_closes_case()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, _) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, []);

        using var response = await client.PostJsonAsync($"/api/cases/{caseId}/resolve", new
        {
            finalOutcomeType = "SellerRejected",
        });
        response.EnsureSuccessStatusCode();

        var (_, detail) = await DetailAsync(client, caseId);
        Assert.Equal("Closed", detail.GetProperty("status").GetString());
        Assert.Contains(
            TimelineSummaries(detail),
            s => s == "Case closed: Seller rejected the request");
    }

    [Fact]
    public async Task Reopen_restores_open_and_clears_final_outcome()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, _) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, []);

        using var reopenEarly = await client.PostJsonAsync($"/api/cases/{caseId}/reopen", new { });
        Assert.Equal(HttpStatusCode.BadRequest, reopenEarly.StatusCode);

        using var resolve = await client.PostJsonAsync($"/api/cases/{caseId}/resolve", new
        {
            finalOutcomeType = "FullRefundReceived",
        });
        resolve.EnsureSuccessStatusCode();

        using var reopen = await client.PostJsonAsync($"/api/cases/{caseId}/reopen", new { });
        reopen.EnsureSuccessStatusCode();

        var (_, detail) = await DetailAsync(client, caseId);
        Assert.Equal("Open", detail.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("finalOutcome").ValueKind);
        Assert.Equal(JsonValueKind.Null, detail.GetProperty("resolvedAt").ValueKind);
        Assert.Contains(TimelineTypes(detail), t => t == "CaseReopened");
        Assert.Contains(TimelineSummaries(detail), s => s == "Case resolved: Full refund received");
    }

    [Fact]
    public async Task Delete_case_keeps_purchase()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);

        using var response = await client.DeleteAsync($"/api/cases/{caseId}");
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        using var purchase = await client.Http.GetAsync($"/api/purchases/{purchaseId}");
        Assert.Equal(HttpStatusCode.OK, purchase.StatusCode);
        var body = await JsonRead.ReadElement(purchase);
        Assert.Empty(body.GetProperty("cases").EnumerateArray());
    }

    [Fact]
    public async Task List_filters_active_resolved_all()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, _) = await PurchaseTests.CreatePurchaseAsync(client);
        var openCase = await PurchaseTests.CreateCaseAsync(client, purchaseId, []);

        var (purchase2, _) = await PurchaseTests.CreatePurchaseAsync(client);
        var resolvedCase = await PurchaseTests.CreateCaseAsync(client, purchase2, []);
        using var resolve = await client.PostJsonAsync($"/api/cases/{resolvedCase}/resolve", new
        {
            finalOutcomeType = "Other",
        });
        resolve.EnsureSuccessStatusCode();

        async Task<Guid[]> IdsFor(string filter)
        {
            using var response = await client.Http.GetAsync($"/api/cases?filter={filter}");
            response.EnsureSuccessStatusCode();
            return (await JsonRead.ReadElement(response)).EnumerateArray()
                .Select(c => c.GetProperty("id").GetGuid())
                .ToArray();
        }

        var active = await IdsFor("active");
        Assert.Contains(openCase, active);
        Assert.DoesNotContain(resolvedCase, active);

        var resolved = await IdsFor("resolved");
        Assert.Contains(resolvedCase, resolved);
        Assert.DoesNotContain(openCase, resolved);

        var all = await IdsFor("all");
        Assert.Contains(openCase, all);
        Assert.Contains(resolvedCase, all);
    }
}
