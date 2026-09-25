using System.Net;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class DocumentTests(ApiFactory factory)
{
    private async Task<(TestClient Client, Guid DocumentId)> UploadAsync()
    {
        var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, _) = await PurchaseTests.CreatePurchaseAsync(client);
        using var upload = await client.PostFileAsync(
            $"/api/purchases/{purchaseId}/documents",
            TestFiles.Pdf, "receipt.pdf", "application/pdf");
        upload.EnsureSuccessStatusCode();
        var doc = await JsonRead.ReadElement(upload);
        return (client, doc.GetProperty("id").GetGuid());
    }

    [Fact]
    public async Task Inline_response_has_security_headers()
    {
        var (client, documentId) = await UploadAsync();
        using var _ = client;

        using var response = await client.Http.GetAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("application/pdf", response.Content.Headers.ContentType?.MediaType);
        var disposition = response.Content.Headers.ContentDisposition;
        Assert.Equal("inline", disposition?.DispositionType);
        Assert.Contains(
            "receipt.pdf",
            response.Content.Headers.GetValues("Content-Disposition").First());
        Assert.Equal("nosniff", response.Headers.GetValues("X-Content-Type-Options").First());
        Assert.Contains("no-store", response.Headers.CacheControl?.ToString());
        Assert.Equal("sandbox", response.Headers.GetValues("Content-Security-Policy").First());
        var bytes = await response.Content.ReadAsByteArrayAsync();
        Assert.Equal(TestFiles.Pdf, bytes);
    }

    [Fact]
    public async Task Download_uses_attachment_disposition()
    {
        var (client, documentId) = await UploadAsync();
        using var _ = client;

        using var response = await client.Http.GetAsync($"/api/documents/{documentId}/download");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.StartsWith(
            "attachment",
            response.Content.Headers.GetValues("Content-Disposition").First());
    }

    [Fact]
    public async Task Anonymous_gets_401()
    {
        var (client, documentId) = await UploadAsync();
        using var _ = client;
        using var anon = factory.CreateClient();

        using var response = await anon.GetAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Other_user_gets_404()
    {
        var (client, documentId) = await UploadAsync();
        using var _ = client;
        using var other = await TestClient.RegisterNewUserAsync(factory);

        using var response = await other.Http.GetAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Delete_removes_row_and_file()
    {
        var filesBefore = factory.StoredFileCount();
        var (client, documentId) = await UploadAsync();
        using var _ = client;

        using var response = await client.DeleteAsync($"/api/documents/{documentId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(filesBefore, factory.StoredFileCount());
        using var gone = await client.Http.GetAsync($"/api/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NotFound, gone.StatusCode);
    }
}
