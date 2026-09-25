using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using ReturnRight.Api.Persistence;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class AccountDeletionTests(ApiFactory factory)
{
    [Fact]
    public async Task Delete_account_removes_rows_files_and_login()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        // Give the user a full data graph: purchase + proof file + case + intake file.
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(client);
        using var docUpload = await client.PostFileAsync(
            $"/api/purchases/{purchaseId}/documents",
            TestFiles.Pdf, "receipt.pdf", "application/pdf");
        docUpload.EnsureSuccessStatusCode();
        await PurchaseTests.CreateCaseAsync(client, purchaseId, itemIds);
        using var intakeUpload = await client.PostFileAsync(
            "/api/intakes", TestFiles.Pdf, "intake.pdf", "application/pdf");
        intakeUpload.EnsureSuccessStatusCode();

        var userId = (await JsonRead.ReadElement(
                await client.Http.GetAsync("/api/auth/me")))
            .GetProperty("id").GetGuid();
        var filesBefore = factory.StoredFileCount();
        Assert.True(filesBefore >= 2);

        using var response = await client.DeleteJsonAsync(
            "/api/profile", new { password = TestClient.Password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        // Both uploaded files must be gone; other users' files remain.
        Assert.Equal(filesBefore - 2, factory.StoredFileCount());

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        Assert.False(await db.Users.AnyAsync(u => u.Email == client.Email));
        Assert.Equal(0, await db.Purchases.CountAsync(p => p.UserId == userId));
        Assert.Equal(0, await db.IssueCases.CountAsync(c => c.UserId == userId));
        Assert.Equal(0, await db.Documents.CountAsync(d => d.UserId == userId));
        Assert.Equal(0, await db.TemporaryIntakes.CountAsync(t => t.UserId == userId));

        using var fresh = await TestClient.RegisterNewUserAsync(factory);
        var login = await fresh.LoginAsync(client.Email, TestClient.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        using var response = await client.DeleteJsonAsync(
            "/api/profile", new { password = "NotThePassword!" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Equal("Your password is incorrect.", JsonRead.ProblemTitle(body));

        using var me = await client.Http.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
    }
}
