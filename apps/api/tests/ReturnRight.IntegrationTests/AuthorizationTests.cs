using System.Net;

namespace ReturnRight.IntegrationTests;

[Collection("Api")]
public sealed class AuthorizationTests(ApiFactory factory)
{
    [Fact]
    public async Task Other_users_resources_return_404_everywhere()
    {
        factory.Extractor.ThrowOnExtract = false;
        using var userA = await TestClient.RegisterNewUserAsync(factory);
        using var userB = await TestClient.RegisterNewUserAsync(factory);

        // User A builds resources.
        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(userA);
        var caseId = await PurchaseTests.CreateCaseAsync(userA, purchaseId, itemIds);

        using var docUpload = await userA.PostFileAsync(
            $"/api/purchases/{purchaseId}/documents",
            TestFiles.Pdf, "receipt.pdf", "application/pdf");
        docUpload.EnsureSuccessStatusCode();
        var documentId = (await JsonRead.ReadElement(docUpload)).GetProperty("id").GetGuid();

        using var intakeUpload = await userA.PostFileAsync(
            "/api/intakes", TestFiles.Pdf, "intake.pdf", "application/pdf");
        intakeUpload.EnsureSuccessStatusCode();
        var intakeId = (await JsonRead.ReadElement(intakeUpload)).GetProperty("id").GetGuid();

        // User B must never see them.
        using var getPurchase = await userB.Http.GetAsync($"/api/purchases/{purchaseId}");
        Assert.Equal(HttpStatusCode.NotFound, getPurchase.StatusCode);

        using var patchPurchase = await userB.PatchJsonAsync($"/api/purchases/{purchaseId}", new
        {
            merchantName = "Evil",
            currency = "USD",
            items = new[] { new { productName = "X", quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.NotFound, patchPurchase.StatusCode);

        using var deletePurchase = await userB.DeleteAsync($"/api/purchases/{purchaseId}");
        Assert.Equal(HttpStatusCode.NotFound, deletePurchase.StatusCode);

        using var purchaseDocs = await userB.PostFileAsync(
            $"/api/purchases/{purchaseId}/documents",
            TestFiles.Pdf, "evil.pdf", "application/pdf");
        Assert.Equal(HttpStatusCode.NotFound, purchaseDocs.StatusCode);

        using var getDocument = await userB.Http.GetAsync($"/api/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NotFound, getDocument.StatusCode);

        using var deleteDocument = await userB.DeleteAsync($"/api/documents/{documentId}");
        Assert.Equal(HttpStatusCode.NotFound, deleteDocument.StatusCode);

        using var getCase = await userB.Http.GetAsync($"/api/cases/{caseId}");
        Assert.Equal(HttpStatusCode.NotFound, getCase.StatusCode);

        using var caseStatus = await userB.PostJsonAsync(
            $"/api/cases/{caseId}/status", new { status = "SellerContacted" });
        Assert.Equal(HttpStatusCode.NotFound, caseStatus.StatusCode);

        using var caseResolve = await userB.PostJsonAsync(
            $"/api/cases/{caseId}/resolve", new { finalOutcomeType = "FullRefundReceived" });
        Assert.Equal(HttpStatusCode.NotFound, caseResolve.StatusCode);

        using var deleteCase = await userB.DeleteAsync($"/api/cases/{caseId}");
        Assert.Equal(HttpStatusCode.NotFound, deleteCase.StatusCode);

        using var getIntake = await userB.Http.GetAsync($"/api/intakes/{intakeId}");
        Assert.Equal(HttpStatusCode.NotFound, getIntake.StatusCode);

        using var confirmIntake = await userB.PostJsonAsync($"/api/intakes/{intakeId}/confirm", new
        {
            merchantName = "Evil",
            currency = "USD",
            items = new[] { new { productName = "X", quantity = 1 } },
        });
        Assert.Equal(HttpStatusCode.NotFound, confirmIntake.StatusCode);

        using var deleteIntake = await userB.DeleteAsync($"/api/intakes/{intakeId}");
        Assert.Equal(HttpStatusCode.NotFound, deleteIntake.StatusCode);
    }

    [Fact]
    public async Task Other_users_case_children_return_404()
    {
        using var userA = await TestClient.RegisterNewUserAsync(factory);
        using var userB = await TestClient.RegisterNewUserAsync(factory);

        var (purchaseId, itemIds) = await PurchaseTests.CreatePurchaseAsync(userA);
        var caseId = await PurchaseTests.CreateCaseAsync(userA, purchaseId, itemIds);

        // Build case children as user A.
        var evidenceForm = new MultipartFormDataContent();
        evidenceForm.Add(TestFiles.FormFile(TestFiles.Png, "image/png"), "file", "a.png");
        evidenceForm.Add(new StringContent("DamagePhoto"), "evidenceType");
        using var evidenceUpload = await userA.SendAsync(
            HttpMethod.Post, $"/api/cases/{caseId}/evidence", evidenceForm);
        evidenceUpload.EnsureSuccessStatusCode();
        var evidenceId = (await JsonRead.ReadElement(evidenceUpload)).GetProperty("id").GetGuid();

        using var interactionPost = await userA.PostJsonAsync(
            $"/api/cases/{caseId}/interactions", new
            {
                interactionType = "ContactedSeller",
                occurredAt = DateTimeOffset.UtcNow,
                note = "hi",
            });
        interactionPost.EnsureSuccessStatusCode();
        var interactionId = (await JsonRead.ReadElement(interactionPost))
            .GetProperty("id").GetGuid();

        using var followUpPost = await userA.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups", new
            {
                title = "Check in",
                dueAt = DateTimeOffset.UtcNow.AddDays(1),
            });
        followUpPost.EnsureSuccessStatusCode();
        var followUpId = (await JsonRead.ReadElement(followUpPost))
            .GetProperty("id").GetGuid();

        // User B gets 404 for every child surface.
        var evidenceFormB = new MultipartFormDataContent();
        evidenceFormB.Add(TestFiles.FormFile(TestFiles.Png, "image/png"), "file", "b.png");
        evidenceFormB.Add(new StringContent("DamagePhoto"), "evidenceType");
        using var evidenceB = await userB.SendAsync(
            HttpMethod.Post, $"/api/cases/{caseId}/evidence", evidenceFormB);
        Assert.Equal(HttpStatusCode.NotFound, evidenceB.StatusCode);

        using var evidencePatch = await userB.PatchJsonAsync(
            $"/api/cases/{caseId}/evidence/{evidenceId}", new { evidenceType = "Other" });
        Assert.Equal(HttpStatusCode.NotFound, evidencePatch.StatusCode);

        using var evidenceDelete = await userB.DeleteAsync(
            $"/api/cases/{caseId}/evidence/{evidenceId}");
        Assert.Equal(HttpStatusCode.NotFound, evidenceDelete.StatusCode);

        using var interactionB = await userB.PostJsonAsync(
            $"/api/cases/{caseId}/interactions", new
            {
                interactionType = "SellerResponded",
                occurredAt = DateTimeOffset.UtcNow,
                note = "sneaky",
            });
        Assert.Equal(HttpStatusCode.NotFound, interactionB.StatusCode);

        using var interactionDelete = await userB.DeleteAsync(
            $"/api/cases/{caseId}/interactions/{interactionId}");
        Assert.Equal(HttpStatusCode.NotFound, interactionDelete.StatusCode);

        using var followUpB = await userB.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups", new
            {
                title = "Sneaky",
                dueAt = DateTimeOffset.UtcNow.AddDays(1),
            });
        Assert.Equal(HttpStatusCode.NotFound, followUpB.StatusCode);

        using var followUpComplete = await userB.PostJsonAsync(
            $"/api/cases/{caseId}/follow-ups/{followUpId}/complete", new { });
        Assert.Equal(HttpStatusCode.NotFound, followUpComplete.StatusCode);

        using var reminders = await userB.Http.GetAsync($"/api/cases/{caseId}/reminders");
        Assert.Equal(HttpStatusCode.NotFound, reminders.StatusCode);

        using var readiness = await userB.Http.GetAsync($"/api/cases/{caseId}/readiness");
        Assert.Equal(HttpStatusCode.NotFound, readiness.StatusCode);

        using var nextAction = await userB.Http.GetAsync($"/api/cases/{caseId}/next-action");
        Assert.Equal(HttpStatusCode.NotFound, nextAction.StatusCode);

        using var dismiss = await userB.PostJsonAsync(
            $"/api/cases/{caseId}/next-action/dismiss", new { key = "schedule_followup" });
        Assert.Equal(HttpStatusCode.NotFound, dismiss.StatusCode);

        using var caseFile = await userB.Http.GetAsync($"/api/cases/{caseId}/case-file");
        Assert.Equal(HttpStatusCode.NotFound, caseFile.StatusCode);

        using var notifications = await userB.Http.GetAsync("/api/notifications");
        notifications.EnsureSuccessStatusCode();
        Assert.Empty((await JsonRead.ReadElement(notifications)).EnumerateArray());
    }
}
