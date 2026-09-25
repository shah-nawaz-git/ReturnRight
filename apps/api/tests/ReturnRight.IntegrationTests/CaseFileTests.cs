using System.Net;
using System.Text;
using UglyToad.PdfPig;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class CaseFileTests(ApiFactory factory)
{
    private static async Task<(TestClient Client, Guid CaseId)> FullCaseAsync(ApiFactory factory)
    {
        var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        var caseId = await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);

        using var doc = await client.PostFileAsync(
            $"/api/purchases/{purchaseId}/documents",
            TestFiles.Pdf, "receipt.pdf", "application/pdf");
        doc.EnsureSuccessStatusCode();

        var form = new MultipartFormDataContent();
        form.Add(TestFiles.FormFile(TestFiles.Png, "image/png"), "file", "damage-photo.png");
        form.Add(new StringContent("DamagePhoto"), "evidenceType");
        form.Add(new StringContent("Cracked headband"), "description");
        using var evidence = await client.SendAsync(
            HttpMethod.Post, $"/api/cases/{caseId}/evidence", form);
        evidence.EnsureSuccessStatusCode();

        using var interaction = await client.PostJsonAsync(
            $"/api/cases/{caseId}/interactions", new
            {
                interactionType = "ContactedSeller",
                occurredAt = DateTimeOffset.UtcNow,
                note = "Emailed the seller about the cracked headband.",
            });
        interaction.EnsureSuccessStatusCode();
        return (client, caseId);
    }

    [Fact]
    public async Task Case_file_is_a_real_pdf_with_case_content()
    {
        var (client, caseId) = await FullCaseAsync(factory);
        using var _ = client;

        using var response = await client.Http.GetAsync($"/api/cases/{caseId}/case-file");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        Assert.Equal("attachment",
            response.Content.Headers.ContentDisposition?.DispositionType);

        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.True(bytes.Length > 5_000, $"PDF too small: {bytes.Length} bytes");
        Assert.Equal("%PDF-", Encoding.ASCII.GetString(bytes[..5]));

        using var pdf = PdfDocument.Open(bytes);
        var text = string.Concat(pdf.GetPages().Select(p => p.Text));

        Assert.Contains("SoundMarket", text);               // merchant
        Assert.Contains("Item 1", text);                    // product
        Assert.Contains("Full refund", text);               // requested outcome
        Assert.Contains("Emailed the seller about the cracked headband", text);
        Assert.Contains("damage-photo.png", text);          // evidence file name
        Assert.Contains("does not certify legal entitlement", text);
    }

    [Fact]
    public async Task Foreign_case_returns_404()
    {
        var (_, caseId) = await FullCaseAsync(factory);
        using var other = await TestClient.RegisterNewUserAsync(factory);

        using var response = await other.Http.GetAsync($"/api/cases/{caseId}/case-file");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Anonymous_request_returns_401()
    {
        var (_, caseId) = await FullCaseAsync(factory);
        using var anonymous = factory.CreateClient(
            new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
            {
                HandleCookies = true,
                AllowAutoRedirect = false,
            });

        using var response = await anonymous.GetAsync($"/api/cases/{caseId}/case-file");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
