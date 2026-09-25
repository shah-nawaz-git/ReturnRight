using System.Net;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class PurchaseTests(ApiFactory factory)
{
    public static async Task<(Guid PurchaseId, Guid[] ItemIds)> CreatePurchaseAsync(
        TestClient client, int itemCount = 2)
    {
        var items = Enumerable.Range(0, itemCount)
            .Select(i => new
            {
                productName = $"Item {i + 1}",
                quantity = 1,
                unitPrice = 10m + i,
            })
            .ToArray();
        using var response = await client.PostJsonAsync("/api/purchases", new
        {
            merchantName = "SoundMarket",
            orderNumber = "SM-48213",
            purchaseDate = "2026-09-10",
            currency = "EUR",
            totalAmount = 389.99m,
            notes = "gift",
            items,
        });
        response.EnsureSuccessStatusCode();
        var body = await JsonRead.ReadElement(response);
        var itemIds = body.GetProperty("items").EnumerateArray()
            .Select(i => i.GetProperty("id").GetGuid())
            .ToArray();
        return (body.GetProperty("id").GetGuid(), itemIds);
    }

    public static async Task<Guid> CreateCaseAsync(
        TestClient client, Guid purchaseId, Guid[] itemIds)
    {
        using var response = await client.PostJsonAsync("/api/cases", new
        {
            problemType = "DamagedItem",
            description = "Arrived broken.",
            requestedOutcomeType = "FullRefund",
            requestedAmount = 389.99m,
            purchaseId,
            affectedItemIds = itemIds,
        });
        response.EnsureSuccessStatusCode();
        return (await JsonRead.ReadElement(response)).GetProperty("id").GetGuid();
    }

    [Fact]
    public async Task Create_with_two_items_then_list_and_detail()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await CreatePurchaseAsync(client);
        Assert.Equal(2, itemIds.Length);

        using var list = await client.Http.GetAsync("/api/purchases");
        list.EnsureSuccessStatusCode();
        var summaries = await JsonRead.ReadElement(list);
        var summary = Assert.Single(summaries.EnumerateArray());
        Assert.Equal("SoundMarket", summary.GetProperty("merchantName").GetString());
        Assert.Equal(2, summary.GetProperty("itemCount").GetInt32());
        Assert.Equal("Item 1", summary.GetProperty("firstItemName").GetString());
        Assert.False(summary.GetProperty("hasProof").GetBoolean());
        Assert.Equal(0, summary.GetProperty("activeCaseCount").GetInt32());

        using var detail = await client.Http.GetAsync($"/api/purchases/{purchaseId}");
        detail.EnsureSuccessStatusCode();
        var body = await JsonRead.ReadElement(detail);
        var provenance = body.GetProperty("provenance").GetProperty("merchantName");
        Assert.Equal("UserEntered", provenance.GetProperty("source").GetString());
        Assert.True(provenance.GetProperty("confirmedByUser").GetBoolean());
        Assert.Equal(2, body.GetProperty("items").GetArrayLength());
    }

    [Fact]
    public async Task Patch_replaces_items_and_updates_fields()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await CreatePurchaseAsync(client);

        using var response = await client.PatchJsonAsync($"/api/purchases/{purchaseId}", new
        {
            merchantName = "SoundMarket B.V.",
            orderNumber = "SM-48213",
            purchaseDate = "2026-09-10",
            currency = "EUR",
            totalAmount = 400.00m,
            notes = "updated",
            items = new object[]
            {
                new { id = itemIds[0], productName = "Item 1 renamed", quantity = 3, unitPrice = 9.99m },
                new { productName = "Brand new item", quantity = 1 },
            },
        });
        response.EnsureSuccessStatusCode();

        var body = await JsonRead.ReadElement(response);
        Assert.Equal("SoundMarket B.V.", body.GetProperty("merchantName").GetString());
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(2, items.Length);
        Assert.Equal("Item 1 renamed", items[0].GetProperty("productName").GetString());
        Assert.Equal(3, items[0].GetProperty("quantity").GetInt32());
        Assert.Equal(items[0].GetProperty("id").GetGuid(), itemIds[0]);
        Assert.Equal(
            "UserEntered",
            body.GetProperty("provenance").GetProperty("merchantName")
                .GetProperty("source").GetString());
    }

    [Fact]
    public async Task Deleting_item_used_by_case_returns_409()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await CreatePurchaseAsync(client);
        await CreateCaseAsync(client, purchaseId, [itemIds[0]]);

        using var response = await client.PatchJsonAsync($"/api/purchases/{purchaseId}", new
        {
            merchantName = "SoundMarket",
            currency = "EUR",
            items = new[] { new { id = itemIds[1], productName = "Item 2", quantity = 1 } },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Equal(
            "This item is part of a case. Remove it from the case first.",
            JsonRead.ProblemTitle(body));
    }

    [Fact]
    public async Task Deleting_purchase_with_case_returns_409()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, itemIds) = await CreatePurchaseAsync(client);
        await CreateCaseAsync(client, purchaseId, itemIds);

        using var response = await client.DeleteAsync($"/api/purchases/{purchaseId}");

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var body = await JsonRead.ReadElement(response);
        Assert.Equal(
            "This purchase is linked to a case. Delete the case first.",
            JsonRead.ProblemTitle(body));
    }

    [Fact]
    public async Task Upload_proof_document_and_delete_purchase_removes_file()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);
        var (purchaseId, _) = await CreatePurchaseAsync(client);
        var filesBefore = factory.StoredFileCount();

        using var upload = await client.PostFileAsync(
            $"/api/purchases/{purchaseId}/documents", TestFiles.Pdf, "receipt.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var doc = await JsonRead.ReadElement(upload);
        Assert.Equal("receipt.pdf", doc.GetProperty("fileName").GetString());
        Assert.Equal("PurchaseDocument", doc.GetProperty("category").GetString());
        Assert.True(factory.StoredFileCount() > filesBefore);

        using var delete = await client.DeleteAsync($"/api/purchases/{purchaseId}");
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(filesBefore, factory.StoredFileCount());
    }

    [Fact]
    public async Task Invalid_currency_and_quantity_rejected()
    {
        using var client = await TestClient.RegisterNewUserAsync(factory);

        using var badCurrency = await client.PostJsonAsync("/api/purchases", new
        {
            merchantName = "Shop",
            currency = "eur",
            items = new[] { new { productName = "X", quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, badCurrency.StatusCode);

        using var badQuantity = await client.PostJsonAsync("/api/purchases", new
        {
            merchantName = "Shop",
            currency = "EUR",
            items = new[] { new { productName = "X", quantity = 0 } },
        });
        Assert.Equal(HttpStatusCode.BadRequest, badQuantity.StatusCode);
        var body = await JsonRead.ReadElement(badQuantity);
        Assert.True(body.TryGetProperty("errors", out _));
    }
}
