using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Intakes;
using ReturnRight.Api.Persistence;
using ReturnRight.Api.Storage;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class IntakeTests(ApiFactory factory)
{
    private sealed class FakeTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private async Task<(Guid Id, System.Text.Json.JsonElement Body)> UploadPdfAsync(TestClient client)
    {
        using var response = await client.PostFileAsync(
            "/api/intakes", TestFiles.Pdf, "receipt.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        return (body.GetProperty("id").GetGuid(), body);
    }

    [Fact]
    public async Task Upload_pdf_extracts_candidates()
    {
        factory.Extractor.ThrowOnExtract = false;
        using var client = await TestClient.RegisterNewUserAsync(factory);

        var (_, body) = await UploadPdfAsync(client);

        Assert.Equal("succeeded", body.GetProperty("status").GetString());
        Assert.Equal("receipt.pdf", body.GetProperty("fileName").GetString());
        var candidates = body.GetProperty("candidates").EnumerateArray().ToArray();
        Assert.Contains(
            candidates,
            c => c.GetProperty("field").GetString() == "merchantName"
                && c.GetProperty("value").GetString() == "SoundMarket");
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Single(items);
        Assert.Equal("Auralis X4 Headphones", items[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task Extractor_failure_marks_intake_failed_but_confirm_still_works()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        factory.Extractor.ThrowOnExtract = true;
        try
        {
            var (intakeId, body) = await UploadPdfAsync(client);
            Assert.Equal("failed", body.GetProperty("status").GetString());
            Assert.Contains("manually", body.GetProperty("message").GetString());

            using var confirm = await client.PostJsonAsync($"/api/intakes/{intakeId}/confirm", new
            {
                merchantName = "ManualShop",
                currency = "USD",
                items = new[] { new { productName = "Thing", quantity = 1 } },
            });
            Assert.Equal(HttpStatusCode.Created, confirm.StatusCode);
            var purchase = await JsonRead.ReadElement(confirm);
            Assert.Equal("ManualShop", purchase.GetProperty("merchantName").GetString());
            Assert.Single(purchase.GetProperty("documents").EnumerateArray());
        }
        finally
        {
            factory.Extractor.ThrowOnExtract = false;
        }
    }

    [Fact]
    public async Task Confirm_records_extracted_provenance()
    {
        factory.Extractor.ThrowOnExtract = false;
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (intakeId, _) = await UploadPdfAsync(client);

        using var confirm = await client.PostJsonAsync($"/api/intakes/{intakeId}/confirm", new
        {
            merchantName = "SoundMarket",
            orderNumber = "SM-48213",
            purchaseDate = "2026-09-10",
            currency = "EUR",
            totalAmount = 389.99m,
            items = new[]
            {
                new
                {
                    productName = "Auralis X4 Headphones",
                    quantity = 1,
                    unitPrice = 389.99m,
                    returnDeadline = "2026-10-10",
                    returnDeadlineSource = "ExtractedFromDocument",
                },
            },
            provenance = new
            {
                merchantName = new { source = "ExtractedFromDocument", confidence = 0.98 },
                purchaseDate = new { source = "ExtractedFromDocument", confidence = 0.91 },
            },
        });
        Assert.Equal(HttpStatusCode.Created, confirm.StatusCode);

        var purchase = await JsonRead.ReadElement(confirm);
        var provenance = purchase.GetProperty("provenance");
        var merchant = provenance.GetProperty("merchantName");
        Assert.Equal("ExtractedFromDocument", merchant.GetProperty("source").GetString());
        Assert.True(merchant.GetProperty("confirmedByUser").GetBoolean());
        Assert.NotEqual(Guid.Empty, merchant.GetProperty("sourceDocumentId").GetGuid());
        Assert.Equal(
            "UserEntered",
            provenance.GetProperty("orderNumber").GetProperty("source").GetString());

        var item = Assert.Single(purchase.GetProperty("items").EnumerateArray());
        Assert.Equal(
            "ExtractedFromDocument",
            item.GetProperty("returnDeadlineProvenance").GetProperty("source").GetString());
    }

    [Fact]
    public async Task Delete_intake_removes_file()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var filesBefore = factory.StoredFileCount();
        var (intakeId, _) = await UploadPdfAsync(client);
        Assert.Equal(filesBefore + 1, factory.StoredFileCount());

        using var response = await client.DeleteAsync($"/api/intakes/{intakeId}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        Assert.Equal(filesBefore, factory.StoredFileCount());
    }

    [Fact]
    public async Task Cleanup_removes_only_expired_intakes_and_their_files()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var now = DateTimeOffset.UtcNow;

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == client.Email);

        var expiredKey = await storage.SaveAsync(
            new MemoryStream(TestFiles.Pdf), ".pdf", CancellationToken.None);
        var freshKey = await storage.SaveAsync(
            new MemoryStream(TestFiles.Pdf), ".pdf", CancellationToken.None);

        db.TemporaryIntakes.AddRange(
            new TemporaryIntake
            {
                UserId = user!.Id,
                StorageKey = expiredKey,
                OriginalFileName = "old.pdf",
                ContentType = "application/pdf",
                SizeBytes = TestFiles.Pdf.Length,
                Status = IntakeStatus.Failed,
                ExpiresAt = now.AddHours(-1),
                CreatedAt = now.AddDays(-1),
            },
            new TemporaryIntake
            {
                UserId = user.Id,
                StorageKey = freshKey,
                OriginalFileName = "new.pdf",
                ContentType = "application/pdf",
                SizeBytes = TestFiles.Pdf.Length,
                Status = IntakeStatus.Succeeded,
                ExpiresAt = now.AddHours(23),
                CreatedAt = now,
            });
        await db.SaveChangesAsync();

        var cleanup = new IntakeCleanupService(db, storage, new FakeTime(now));
        var removed = await cleanup.CleanupExpiredAsync(CancellationToken.None);

        var localStorage = (LocalFileStorage)storage;
        Assert.Equal(1, removed);
        Assert.False(File.Exists(localStorage.ResolvePath(expiredKey)));
        Assert.True(File.Exists(localStorage.ResolvePath(freshKey)));
        Assert.Equal(1, await db.TemporaryIntakes.CountAsync());

        // Tidy the leftover intake + file so later storage-count assertions stay clean.
        var remaining = await db.TemporaryIntakes.SingleAsync();
        db.TemporaryIntakes.Remove(remaining);
        await db.SaveChangesAsync();
        await storage.DeleteAsync(freshKey, CancellationToken.None);
    }

    [Fact]
    public async Task Executable_named_pdf_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        using var response = await client.PostFileAsync(
            "/api/intakes", TestFiles.ExeBytes, "definitely.pdf", "application/pdf");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.True(body.TryGetProperty("errors", out var errors));
        Assert.Contains("PDF, JPG and PNG", errors.ToString());
    }

    [Fact]
    public async Task Oversize_upload_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var big = new byte[10 * 1024 * 1024 + 1];
        Array.Copy(TestFiles.Pdf, big, TestFiles.Pdf.Length);

        using var response = await client.PostFileAsync(
            "/api/intakes", big, "huge.pdf", "application/pdf");

        Assert.True(
            response.StatusCode is HttpStatusCode.BadRequest
                or HttpStatusCode.RequestEntityTooLarge,
            $"Expected 400/413, got {(int)response.StatusCode}");
    }
}
