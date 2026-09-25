using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ReturnRight.Api.Domain;

namespace ReturnRight.Api.Persistence;

/// <summary>Development/demo seed — idempotent, enabled via Seed:Enabled + Seed:DemoPassword.</summary>
public static class DevSeeder
{
    public const string DemoEmail = "demo@returnright.local";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var configuration = services.GetRequiredService<IConfiguration>();
        var password = configuration["Seed:DemoPassword"];
        if (!configuration.GetValue("Seed:Enabled", true) || string.IsNullOrEmpty(password))
        {
            return;
        }

        var userManager = services.GetRequiredService<UserManager<AppUser>>();
        var db = services.GetRequiredService<AppDbContext>();
        var time = services.GetRequiredService<TimeProvider>();
        var logger = services.GetRequiredService<ILoggerFactory>()
            .CreateLogger("ReturnRight.DevSeeder");

        if (await userManager.FindByEmailAsync(DemoEmail) is not null)
        {
            return;
        }

        var now = time.GetUtcNow();
        var user = new AppUser
        {
            UserName = DemoEmail,
            Email = DemoEmail,
            EmailConfirmed = true,
            CreatedAt = now,
        };
        var created = await userManager.CreateAsync(user, password);
        if (!created.Succeeded)
        {
            logger.LogWarning(
                "Demo user seed failed: {Errors}",
                string.Join("; ", created.Errors.Select(e => e.Code)));
            return;
        }

        var purchase = new Purchase
        {
            UserId = user.Id,
            MerchantName = "SoundMarket",
            OrderNumber = "SM-48213",
            PurchaseDate = new DateOnly(2026, 9, 10),
            Currency = "EUR",
            TotalAmount = 389.99m,
            MerchantNameProvenance = FieldProvenance.User(),
            OrderNumberProvenance = FieldProvenance.User(),
            PurchaseDateProvenance = FieldProvenance.User(),
            TotalAmountProvenance = FieldProvenance.User(),
            CreatedAt = now,
            UpdatedAt = now,
        };
        var headphones = new PurchaseItem
        {
            PurchaseId = purchase.Id,
            ProductName = "Auralis X4 Headphones",
            Quantity = 1,
            UnitPrice = 389.99m,
            ReturnDeadline = new DateOnly(2026, 10, 10),
            ReturnDeadlineProvenance = FieldProvenance.User(),
            CommercialWarrantyEndProvenance = FieldProvenance.User(),
            SortOrder = 0,
            CreatedAt = now,
        };
        purchase.Items.Add(headphones);
        db.Purchases.Add(purchase);

        var issueCase = new IssueCase
        {
            UserId = user.Id,
            PurchaseId = purchase.Id,
            ProblemType = ProblemType.DamagedItem,
            Description =
                "Arrived with a cracked headband and the left cup doesn't produce sound.",
            ProblemDiscoveredOn = new DateOnly(2026, 9, 12),
            Status = CaseStatus.WaitingForSeller,
            RequestedOutcomeType = RequestedOutcomeType.FullRefund,
            RequestedAmount = 389.99m,
            RequestedCurrency = "EUR",
            OutcomeRequestedAt = now,
            CreatedAt = new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero),
            UpdatedAt = now,
        };
        issueCase.AffectedItems.Add(new CaseAffectedItem
        {
            CaseId = issueCase.Id,
            PurchaseItemId = headphones.Id,
        });

        var storage = services.GetRequiredService<ReturnRight.Api.Storage.IFileStorage>();

        // Chronological history: the case was opened and the purchase attached
        // (with its receipt) when the problem was discovered on 12 Sep.
        var discoveredAt = new DateTimeOffset(2026, 9, 12, 9, 0, 0, TimeSpan.Zero);
        Timeline(issueCase, TimelineEventType.CaseCreated, discoveredAt, "Case created");
        Timeline(issueCase, TimelineEventType.PurchaseLinked, discoveredAt,
            "Linked purchase from SoundMarket");

        var receiptKey = await storage.SaveAsync(
            new MemoryStream(ReceiptPdf), ".pdf", CancellationToken.None);
        var receipt = new Document
        {
            UserId = user.Id,
            PurchaseId = purchase.Id,
            StorageKey = receiptKey,
            OriginalFileName = "receipt.pdf",
            ContentType = "application/pdf",
            SizeBytes = ReceiptPdf.Length,
            Category = DocumentCategory.PurchaseDocument,
            CreatedAt = discoveredAt,
        };
        db.Documents.Add(receipt);
        issueCase.Evidence.Add(new Evidence
        {
            CaseId = issueCase.Id,
            DocumentId = receipt.Id,
            EvidenceType = EvidenceType.PurchaseProof,
            CreatedAt = discoveredAt,
        });
        Timeline(issueCase, TimelineEventType.EvidenceAdded, discoveredAt,
            "Purchase proof added: receipt.pdf");

        var contactedAt = new DateTimeOffset(2026, 9, 12, 10, 15, 0, TimeSpan.Zero);
        var contacted = new Interaction
        {
            CaseId = issueCase.Id,
            InteractionType = InteractionType.ContactedSeller,
            OccurredAt = contactedAt,
            Note = "Emailed the seller about the cracked headband and dead left cup. "
                + "Asked for a full refund and attached photos.",
            CreatedAt = contactedAt,
            UpdatedAt = contactedAt,
        };
        issueCase.Interactions.Add(contacted);
        Timeline(issueCase, TimelineEventType.InteractionAdded, contactedAt,
            "Seller contacted", new { note = Truncate(contacted.Note) });
        Timeline(issueCase, TimelineEventType.StatusChanged, contactedAt,
            "Status changed to Seller contacted");

        var askedAt = new DateTimeOffset(2026, 9, 13, 9, 5, 0, TimeSpan.Zero);
        var asked = new Interaction
        {
            CaseId = issueCase.Id,
            InteractionType = InteractionType.SellerRequestedInformation,
            OccurredAt = askedAt,
            Note = "Seller asked for the order number and a photo of the shipping label.",
            CreatedAt = askedAt,
            UpdatedAt = askedAt,
        };
        issueCase.Interactions.Add(asked);
        Timeline(issueCase, TimelineEventType.InteractionAdded, askedAt,
            "Seller requested information", new { note = Truncate(asked.Note) });
        Timeline(issueCase, TimelineEventType.StatusChanged, askedAt,
            "Status changed to Waiting for seller");
        db.IssueCases.Add(issueCase);

        // Two damage photos stored like real uploads (document + file + evidence),
        // added the day after the case was opened.
        var evidenceAt = new DateTimeOffset(2026, 9, 13, 14, 20, 0, TimeSpan.Zero);
        var followUpDue = now.AddDays(3);
        var followUp = new FollowUp
        {
            // Set explicitly: the entity is only tracked on SaveChanges via the
            // collection nav, but the reminders below need the key value now.
            Id = Guid.NewGuid(),
            CaseId = issueCase.Id,
            Title = "Check whether the seller replied",
            DueAt = followUpDue,
            CreatedAt = now,
        };
        issueCase.FollowUps.Add(followUp);
        Timeline(issueCase, TimelineEventType.FollowUpCreated, now,
            $"Follow-up scheduled for {followUpDue:d MMM yyyy}: {followUp.Title}");

        var photoSpecs = new (string Name, byte R, byte G, byte B, string Description)[]
        {
            ("cracked-headband.png", 0xC7, 0x39, 0x39, "Crack running across the headband"),
            ("silent-left-cup.png", 0x31, 0x5C, 0x6B, "Left earcup where no sound comes out"),
        };
        foreach (var (name, r, g, b, description) in photoSpecs)
        {
            var png = SeedPng.SolidColor(240, 160, r, g, b);
            var key = await storage.SaveAsync(
                new MemoryStream(png), ".png", CancellationToken.None);
            var document = new Document
            {
                UserId = user.Id,
                StorageKey = key,
                OriginalFileName = name,
                ContentType = "image/png",
                SizeBytes = png.Length,
                Category = DocumentCategory.Evidence,
                CreatedAt = evidenceAt,
            };
            db.Documents.Add(document);
            issueCase.Evidence.Add(new Evidence
            {
                CaseId = issueCase.Id,
                DocumentId = document.Id,
                EvidenceType = EvidenceType.DamagePhoto,
                Description = description,
                CreatedAt = evidenceAt,
            });
            Timeline(issueCase, TimelineEventType.EvidenceAdded, evidenceAt,
                $"Photo added: {name}");
        }

        // Reminders for the follow-up (both channels — demo prefs default to enabled).
        foreach (var channel in new[] { ReminderChannel.Email, ReminderChannel.InApp })
        {
            db.Reminders.Add(new Reminder
            {
                UserId = user.Id,
                CaseId = issueCase.Id,
                FollowUpId = followUp.Id,
                Channel = channel,
                ScheduledFor = followUpDue,
                NextAttemptAt = followUpDue,
                Status = ReminderStatus.Scheduled,
                CreatedAt = now,
            });
        }

        // A second, already-resolved case so history and the "resolved" state are visible.
        var resolvedCase = new IssueCase
        {
            UserId = user.Id,
            ProblemType = ProblemType.DefectiveItem,
            Description =
                "The Nordlys Desk Lamp flickers and shuts off after a few minutes. "
                + "Lumen&Co approved a replacement which arrived working.",
            ProblemDiscoveredOn = new DateOnly(2026, 7, 20),
            Status = CaseStatus.Resolved,
            RequestedOutcomeType = RequestedOutcomeType.Replacement,
            FinalOutcomeType = FinalOutcomeType.ReplacementReceived,
            FinalOutcomeOn = new DateOnly(2026, 8, 2),
            FinalNote = "Replacement lamp received and works correctly.",
            OutcomeRequestedAt = now.AddDays(-50),
            OutcomeCompletedAt = now.AddDays(-40),
            CreatedAt = now.AddDays(-60),
            UpdatedAt = now.AddDays(-40),
        };
        resolvedCase.Purchase = new Purchase
        {
            UserId = user.Id,
            MerchantName = "Lumen&Co",
            OrderNumber = "LC-90117",
            PurchaseDate = new DateOnly(2026, 7, 15),
            Currency = "EUR",
            TotalAmount = 89.00m,
            MerchantNameProvenance = FieldProvenance.User(),
            OrderNumberProvenance = FieldProvenance.User(),
            PurchaseDateProvenance = FieldProvenance.User(),
            TotalAmountProvenance = FieldProvenance.User(),
            CreatedAt = now.AddDays(-60),
            UpdatedAt = now.AddDays(-60),
        };
        var lamp = new PurchaseItem
        {
            ProductName = "Nordlys Desk Lamp",
            Quantity = 1,
            UnitPrice = 89.00m,
            ReturnDeadlineProvenance = FieldProvenance.User(),
            CommercialWarrantyEndProvenance = FieldProvenance.User(),
            SortOrder = 0,
            CreatedAt = now.AddDays(-60),
        };
        resolvedCase.Purchase.Items.Add(lamp);
        resolvedCase.AffectedItems.Add(new CaseAffectedItem
        {
            CaseId = resolvedCase.Id,
            PurchaseItem = lamp,
        });
        Timeline(resolvedCase, TimelineEventType.CaseCreated, now.AddDays(-55),
            "Case created");
        Timeline(resolvedCase, TimelineEventType.InteractionAdded, now.AddDays(-54),
            "Seller contacted");
        Timeline(resolvedCase, TimelineEventType.CaseResolved, now.AddDays(-40),
            "Case resolved: Replacement received");
        db.IssueCases.Add(resolvedCase);

        db.InAppNotifications.Add(new InAppNotification
        {
            UserId = user.Id,
            CaseId = resolvedCase.Id,
            Title = "Case resolved",
            Body = "Your Nordlys Desk Lamp case was marked resolved.",
            CreatedAt = now.AddDays(-40),
            ReadAt = now.AddDays(-39),
        });
        db.InAppNotifications.Add(new InAppNotification
        {
            UserId = user.Id,
            CaseId = issueCase.Id,
            Title = "Follow-up scheduled",
            Body = $"We'll remind you on {followUpDue:d MMM yyyy} to check in with the seller.",
            CreatedAt = now,
        });

        await db.SaveChangesAsync();
        logger.LogInformation("Seeded demo account {Email}", DemoEmail);
    }

    private static void Timeline(
        IssueCase issueCase, TimelineEventType type, DateTimeOffset at,
        string summary, object? metadata = null) =>
        issueCase.TimelineEvents.Add(new CaseTimelineEvent
        {
            CaseId = issueCase.Id,
            EventType = type,
            OccurredAt = at,
            Summary = summary,
            MetadataJson = metadata is null
                ? null
                : System.Text.Json.JsonSerializer.Serialize(metadata),
            CreatedAt = at,
        });

    private static string Truncate(string text) =>
        text.Length <= 120 ? text : text[..120];

    private static readonly byte[] ReceiptPdf =
        "%PDF-1.4\n1 0 obj<</Type/Catalog>>endobj\ntrailer<</Root 1 0 R>>\n%%EOF\n"u8.ToArray();
}
