using System.Net;
using System.Text.Json;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class EvidenceTests(ApiFactory factory)
{
    private static KeyValuePair<string, string>[] Fields(
        string evidenceType, string? description = null)
    {
        var fields = new List<KeyValuePair<string, string>>
        {
            new("evidenceType", evidenceType),
        };
        if (description is not null)
        {
            fields.Add(new("description", description));
        }
        return fields.ToArray();
    }

    private static async Task<(Guid CaseId, JsonElement Detail)> DetailAsync(
        TestClient client, Guid caseId)
    {
        using var response = await client.Http.GetAsync($"/api/cases/{caseId}");
        response.EnsureSuccessStatusCode();
        return (caseId, await JsonRead.ReadElement(response));
    }

    private static JsonElement[] Timeline(JsonElement detail) =>
        detail.GetProperty("timeline").EnumerateArray().ToArray();

    private static JsonElement EvidenceItem(JsonElement detail, string fileName) =>
        detail.GetProperty("evidence").EnumerateArray()
            .Single(e => e.GetProperty("document").GetProperty("fileName").GetString() == fileName);

    private static async Task<Guid> NewCaseAsync(TestClient client)
    {
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        return await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);
    }

    [Fact]
    public async Task Upload_png_evidence_appears_in_detail_timeline_and_readiness()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        using var response = await client.PostFormAsync(
            $"/api/cases/{caseId}/evidence",
            TestFiles.Png, "damage.png", "image/png",
            Fields("DamagePhoto", "Cracked housing"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Equal("DamagePhoto", body.GetProperty("evidenceType").GetString());
        Assert.Equal("damage.png",
            body.GetProperty("document").GetProperty("fileName").GetString());
        Assert.Equal("Cracked housing", body.GetProperty("description").GetString());

        var (_, detail) = await DetailAsync(client, caseId);
        EvidenceItem(detail, "damage.png");
        Assert.Contains(
            Timeline(detail),
            e => e.GetProperty("eventType").GetString() == "EvidenceAdded"
                && e.GetProperty("summary").GetString() == "Photo added: damage.png");

        var supporting = detail.GetProperty("readiness").GetProperty("items")
            .EnumerateArray().Single(i => i.GetProperty("key").GetString() == "supporting_evidence");
        Assert.True(supporting.GetProperty("isComplete").GetBoolean());
    }

    [Fact]
    public async Task Patch_evidence_updates_type_and_description()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        using var upload = await client.PostFormAsync(
            $"/api/cases/{caseId}/evidence",
            TestFiles.Png, "note.png", "image/png", Fields("Other"));
        upload.EnsureSuccessStatusCode();
        var evidenceId = (await JsonRead.ReadElement(upload)).GetProperty("id").GetGuid();

        using var patch = await client.PatchJsonAsync(
            $"/api/cases/{caseId}/evidence/{evidenceId}", new
            {
                evidenceType = "SellerCommunication",
                description = "Screenshot of the seller's reply",
            });
        patch.EnsureSuccessStatusCode();
        var patched = await JsonRead.ReadElement(patch);
        Assert.Equal("SellerCommunication", patched.GetProperty("evidenceType").GetString());
        Assert.Equal("Screenshot of the seller's reply",
            patched.GetProperty("description").GetString());
    }

    [Fact]
    public async Task Delete_evidence_removes_document_and_file()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        using var upload = await client.PostFormAsync(
            $"/api/cases/{caseId}/evidence",
            TestFiles.Png, "photo.png", "image/png", Fields("DamagePhoto"));
        upload.EnsureSuccessStatusCode();
        var evidenceId = (await JsonRead.ReadElement(upload)).GetProperty("id").GetGuid();
        var before = factory.StoredFileCount();

        using var delete = await client.DeleteAsync($"/api/cases/{caseId}/evidence/{evidenceId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(before - 1, factory.StoredFileCount());

        var (_, detail) = await DetailAsync(client, caseId);
        Assert.DoesNotContain(
            detail.GetProperty("evidence").EnumerateArray(),
            e => e.GetProperty("id").GetGuid() == evidenceId);
        Assert.Contains(
            Timeline(detail),
            e => e.GetProperty("eventType").GetString() == "EvidenceRemoved"
                && e.GetProperty("summary").GetString() == "Removed photo.png");
    }

    [Fact]
    public async Task Exe_disguised_as_png_is_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        using var response = await client.PostFormAsync(
            $"/api/cases/{caseId}/evidence",
            TestFiles.ExeBytes, "evil.png", "image/png", Fields("DamagePhoto"));
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Resolved_case_rejects_evidence_changes()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var caseId = await NewCaseAsync(client);

        using var upload = await client.PostFormAsync(
            $"/api/cases/{caseId}/evidence",
            TestFiles.Png, "photo.png", "image/png", Fields("DamagePhoto"));
        upload.EnsureSuccessStatusCode();
        var evidenceId = (await JsonRead.ReadElement(upload)).GetProperty("id").GetGuid();

        using var resolve = await client.PostJsonAsync($"/api/cases/{caseId}/resolve", new
        {
            finalOutcomeType = "FullRefundReceived",
        });
        resolve.EnsureSuccessStatusCode();

        using var add = await client.PostFormAsync(
            $"/api/cases/{caseId}/evidence",
            TestFiles.Png, "more.png", "image/png", Fields("DamagePhoto"));
        Assert.Equal(HttpStatusCode.Conflict, add.StatusCode);
        var body = await JsonRead.ReadElement(add);
        Assert.Equal("This case is resolved. Reopen it to add updates.",
            JsonRead.ProblemTitle(body));

        using var patch = await client.PatchJsonAsync(
            $"/api/cases/{caseId}/evidence/{evidenceId}", new
            {
                evidenceType = "Other",
            });
        Assert.Equal(HttpStatusCode.Conflict, patch.StatusCode);

        using var delete = await client.DeleteAsync($"/api/cases/{caseId}/evidence/{evidenceId}");
        Assert.Equal(HttpStatusCode.Conflict, delete.StatusCode);
    }
}
