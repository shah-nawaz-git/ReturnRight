using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases.Readiness;

namespace ReturnRight.UnitTests;

public class CaseReadinessServiceTests
{
    private static IssueCase DamagedCase() => new()
    {
        UserId = Guid.NewGuid(),
        ProblemType = ProblemType.DamagedItem,
        Description = "Arrived with a cracked headband and no sound on the left.",
        RequestedOutcomeType = RequestedOutcomeType.FullRefund,
        RequestedAmount = 389.99m,
        RequestedCurrency = "EUR",
        Purchase = new Purchase
        {
            MerchantName = "SoundMarket",
            Currency = "EUR",
            OrderNumber = "SM-48213",
            PurchaseDate = new DateOnly(2026, 9, 10),
            MerchantNameProvenance = FieldProvenance.User(),
            OrderNumberProvenance = FieldProvenance.User(),
            PurchaseDateProvenance = FieldProvenance.User(),
            TotalAmountProvenance = FieldProvenance.User(),
        },
    };

    private static Document Doc(string name) => new()
    {
        OriginalFileName = name,
        StorageKey = $"k-{name}",
        ContentType = "image/png",
    };

    private static ReadinessItem Item(IssueCase issueCase, string key) =>
        CaseReadinessService.Evaluate(issueCase).Items.Single(i => i.Key == key);

    [Fact]
    public void Damaged_case_with_everything_is_complete()
    {
        var issueCase = DamagedCase();
        issueCase.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.DamagePhoto,
            Document = Doc("a.png"),
        });
        issueCase.Interactions.Add(new Interaction { Note = "Emailed the seller." });
        issueCase.Purchase!.Documents.Add(Doc("receipt.pdf"));

        var readiness = CaseReadinessService.Evaluate(issueCase);

        Assert.Equal(6, readiness.Total);
        Assert.Equal(6, readiness.Completed);
        Assert.All(readiness.Items, i => Assert.True(i.IsComplete));
    }

    [Fact]
    public void Damaged_case_missing_pieces_is_incomplete()
    {
        var issueCase = DamagedCase();
        issueCase.Purchase = null;
        issueCase.RequestedAmount = null; // FullRefund without amount → incomplete
        issueCase.Description = "short";

        var readiness = CaseReadinessService.Evaluate(issueCase);

        Assert.Equal(0, readiness.Completed);
        Assert.False(Item(issueCase, "purchase_proof").IsComplete);
        Assert.False(Item(issueCase, "order_reference").IsComplete);
        Assert.False(Item(issueCase, "problem_description").IsComplete);
        Assert.False(Item(issueCase, "requested_outcome").IsComplete);
        Assert.False(Item(issueCase, "supporting_evidence").IsComplete);
        Assert.False(Item(issueCase, "seller_contact").IsComplete);
    }

    [Fact]
    public void Damage_photo_flips_supporting_evidence()
    {
        var issueCase = DamagedCase();
        Assert.False(Item(issueCase, "supporting_evidence").IsComplete);

        issueCase.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.DamagePhoto,
            Document = Doc("a.png"),
        });
        Assert.True(Item(issueCase, "supporting_evidence").IsComplete);
    }

    [Fact]
    public void Interaction_flips_seller_contact()
    {
        var issueCase = DamagedCase();
        Assert.False(Item(issueCase, "seller_contact").IsComplete);

        issueCase.Interactions.Add(new Interaction { Note = "Called them." });
        Assert.True(Item(issueCase, "seller_contact").IsComplete);
    }

    [Fact]
    public void Refund_problem_needs_refund_details_and_skips_order_reference()
    {
        var issueCase = DamagedCase();
        issueCase.ProblemType = ProblemType.RefundProblem;
        issueCase.Purchase!.OrderNumber = null;
        issueCase.Purchase.Documents.Add(Doc("receipt.pdf"));
        issueCase.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.RefundConfirmation,
            Document = Doc("r.png"),
        });
        issueCase.Interactions.Add(new Interaction { Note = "Asked again." });

        var readiness = CaseReadinessService.Evaluate(issueCase);

        Assert.DoesNotContain(readiness.Items, i => i.Key == "order_reference");
        var refund = Item(issueCase, "refund_details");
        Assert.False(refund.IsComplete);

        issueCase.OutcomeExpectedBy = new DateOnly(2026, 10, 1);
        readiness = CaseReadinessService.Evaluate(issueCase);
        Assert.Equal(readiness.Total, readiness.Completed);
    }

    [Fact]
    public void Refund_details_completed_by_promised_at()
    {
        var issueCase = DamagedCase();
        issueCase.ProblemType = ProblemType.RefundProblem;
        Assert.False(Item(issueCase, "refund_details").IsComplete);

        issueCase.OutcomePromisedAt = DateTimeOffset.UtcNow;
        Assert.True(Item(issueCase, "refund_details").IsComplete);
    }

    [Fact]
    public void Other_type_uses_generic_supporting_evidence()
    {
        var issueCase = DamagedCase();
        issueCase.ProblemType = ProblemType.Other;

        var readiness = CaseReadinessService.Evaluate(issueCase);
        Assert.DoesNotContain(readiness.Items, i => i.Key == "order_reference");
        Assert.DoesNotContain(readiness.Items, i => i.Key == "refund_details");

        var supporting = Item(issueCase, "supporting_evidence");
        Assert.Equal("Supporting evidence", supporting.Label);
        Assert.False(supporting.IsComplete);

        // Purchase-proof evidence alone does not satisfy "Other" supporting evidence.
        issueCase.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.PurchaseProof,
            Document = Doc("r.pdf"),
        });
        Assert.False(Item(issueCase, "supporting_evidence").IsComplete);

        issueCase.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.Other,
            Document = Doc("note.png"),
        });
        Assert.True(Item(issueCase, "supporting_evidence").IsComplete);
    }
}
