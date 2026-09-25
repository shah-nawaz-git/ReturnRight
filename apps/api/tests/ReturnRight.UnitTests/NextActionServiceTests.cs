using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases.NextAction;
using ReturnRight.Api.Features.Cases.Readiness;

namespace ReturnRight.UnitTests;

public class NextActionServiceTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    /// <summary>A case with proof, outcome and seller contact — rules 2–4 satisfied.</summary>
    private static IssueCase WellPrepared(
        CaseStatus status = CaseStatus.SellerContacted) => new()
        {
            UserId = Guid.NewGuid(),
            ProblemType = ProblemType.DamagedItem,
            Description = "Arrived with a cracked headband and no sound on the left.",
            RequestedOutcomeType = RequestedOutcomeType.FullRefund,
            RequestedAmount = 389.99m,
            RequestedCurrency = "EUR",
            Status = status,
            UpdatedAt = Now.AddDays(-1),
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

    private static void MakeComplete(IssueCase issueCase)
    {
        issueCase.Purchase!.Documents.Add(new Document
        {
            OriginalFileName = "receipt.pdf",
            StorageKey = "k",
            ContentType = "application/pdf",
        });
        issueCase.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.DamagePhoto,
            Document = new Document
            {
                OriginalFileName = "a.png",
                StorageKey = "k2",
                ContentType = "image/png",
            },
        });
        issueCase.Interactions.Add(new Interaction
        {
            InteractionType = InteractionType.ContactedSeller,
            OccurredAt = Now.AddDays(-2),
            Note = "Emailed the seller.",
        });
    }

    private static List<NextActionResponse> Evaluate(IssueCase issueCase) =>
        NextActionService.Evaluate(issueCase, CaseReadinessService.Evaluate(issueCase), Now);

    [Fact]
    public void Rule1_resolved_case_reports_resolved()
    {
        var issueCase = WellPrepared(CaseStatus.Resolved);
        issueCase.FinalOutcomeType = FinalOutcomeType.FullRefundReceived;
        issueCase.FinalAmount = 389.99m;
        issueCase.FinalCurrency = "EUR";
        issueCase.FinalOutcomeOn = new DateOnly(2026, 9, 30);

        var candidates = Evaluate(issueCase);

        var action = Assert.Single(candidates);
        Assert.Equal("resolved", action.Key);
        Assert.Equal("This case is resolved", action.Title);
        Assert.Contains("Full refund received", action.Description);
        Assert.Contains("389.99 EUR", action.Description);
        Assert.False(action.IsDismissible);
    }

    [Fact]
    public void Rule1_closed_case_without_outcome()
    {
        var issueCase = WellPrepared(CaseStatus.Closed);

        var action = Assert.Single(Evaluate(issueCase));
        Assert.Equal("This case is closed", action.Title);
        Assert.Equal("No final outcome recorded.", action.Description);
    }

    [Fact]
    public void Rule2_missing_proof_suggests_add_proof()
    {
        var issueCase = WellPrepared();
        Assert.Equal("add_proof", Evaluate(issueCase)[0].Key);
    }

    [Fact]
    public void Rule3_refund_without_amount_suggests_set_outcome()
    {
        var issueCase = WellPrepared();
        issueCase.RequestedAmount = null;
        issueCase.Purchase!.Documents.Add(new Document
        {
            OriginalFileName = "receipt.pdf",
            StorageKey = "k",
            ContentType = "application/pdf",
        });

        Assert.Equal("set_outcome", Evaluate(issueCase)[0].Key);
    }

    [Fact]
    public void Rule4_fresh_case_suggests_contact_seller()
    {
        var issueCase = WellPrepared();
        MakeComplete(issueCase);
        issueCase.Interactions.Clear();

        Assert.Equal("contact_seller", Evaluate(issueCase)[0].Key);
    }

    [Fact]
    public void Rule5_overdue_follow_up_wins()
    {
        var issueCase = WellPrepared();
        MakeComplete(issueCase);
        var followUp = new FollowUp
        {
            Title = "Check in",
            DueAt = Now.AddDays(-1),
        };
        issueCase.FollowUps.Add(followUp);

        var action = Evaluate(issueCase)[0];
        Assert.Equal($"followup_overdue:{followUp.Id}", action.Key);
        Assert.Equal("followup_overdue", action.Kind);
        Assert.Equal(followUp.Id, action.FollowUpId);
    }

    [Fact]
    public void Rule6_expected_outcome_date_passed()
    {
        var issueCase = WellPrepared(CaseStatus.RefundPending);
        MakeComplete(issueCase);
        issueCase.OutcomeExpectedBy = DateOnly.FromDateTime(Now.AddDays(-1).DateTime);

        var action = Evaluate(issueCase)[0];
        Assert.Equal("outcome_expected_passed", action.Key);
        Assert.Contains("refund", action.Title);
    }

    [Fact]
    public void Rule6_skipped_when_outcome_already_completed()
    {
        var issueCase = WellPrepared(CaseStatus.RefundPending);
        MakeComplete(issueCase);
        issueCase.OutcomeExpectedBy = DateOnly.FromDateTime(Now.AddDays(-1).DateTime);
        issueCase.OutcomeCompletedAt = Now;

        Assert.DoesNotContain(Evaluate(issueCase), c => c.Kind == "outcome_expected_passed");
    }

    [Fact]
    public void Rule7_waiting_status_with_future_follow_up()
    {
        var issueCase = WellPrepared(CaseStatus.WaitingForSeller);
        MakeComplete(issueCase);
        var followUp = new FollowUp
        {
            Title = "Check in",
            DueAt = Now.AddDays(3),
        };
        issueCase.FollowUps.Add(followUp);

        var action = Evaluate(issueCase)[0];
        Assert.Equal($"waiting:{followUp.Id}", action.Key);
        Assert.Equal("Waiting for seller response", action.Title);
        Assert.Equal("ChangeFollowUp", action.ActionType);
    }

    [Fact]
    public void Rule7_uses_latest_timeline_event_not_updated_at()
    {
        var issueCase = WellPrepared(CaseStatus.WaitingForSeller);
        MakeComplete(issueCase);
        issueCase.FollowUps.Add(new FollowUp { Title = "Check in", DueAt = Now.AddDays(3) });
        // UpdatedAt is yesterday; the latest event is 10 days ago.
        issueCase.TimelineEvents.Add(new CaseTimelineEvent
        {
            EventType = TimelineEventType.InteractionAdded,
            OccurredAt = Now.AddDays(-10),
            Summary = "Seller contacted",
        });

        var action = Evaluate(issueCase)[0];
        Assert.Contains($"last updated this case on {Now.AddDays(-10):d MMM}", action.Description);
    }

    [Fact]
    public void Rule8_first_incomplete_readiness_item_is_dismissible()
    {
        var issueCase = WellPrepared();
        MakeComplete(issueCase);
        issueCase.Purchase!.OrderNumber = null; // order_reference now incomplete

        var action = Evaluate(issueCase)[0];
        Assert.Equal("readiness:order_reference", action.Key);
        Assert.Equal("Add order or reference number", action.Title);
        Assert.Equal("EditCase", action.ActionType);
        Assert.True(action.IsDismissible);
    }

    [Fact]
    public void Rule8_supporting_evidence_maps_to_add_evidence()
    {
        var issueCase = WellPrepared();
        MakeComplete(issueCase);
        issueCase.Evidence.Clear(); // supporting_evidence now incomplete

        var action = Evaluate(issueCase)[0];
        Assert.Equal("readiness:supporting_evidence", action.Key);
        Assert.Equal("AddEvidence", action.ActionType);
    }

    [Fact]
    public void Rule9_complete_case_suggests_scheduling_a_follow_up()
    {
        var issueCase = WellPrepared();
        MakeComplete(issueCase);

        var action = Evaluate(issueCase)[0];
        Assert.Equal("schedule_followup", action.Key);
        Assert.Equal("ScheduleFollowUp", action.ActionType);
        Assert.True(action.IsDismissible);
    }

    [Fact]
    public void Rule10_up_to_date_fallback()
    {
        var issueCase = WellPrepared(CaseStatus.Open); // not in the waiting set
        MakeComplete(issueCase);
        issueCase.FollowUps.Add(new FollowUp { Title = "t", DueAt = Now.AddDays(3) });

        var action = Evaluate(issueCase)[0];
        Assert.Equal("up_to_date", action.Key);
        Assert.False(action.IsDismissible);
    }

    [Fact]
    public void Dismissed_readiness_key_is_skipped()
    {
        var issueCase = WellPrepared();
        MakeComplete(issueCase);
        issueCase.Purchase!.OrderNumber = null;
        issueCase.DismissedNextActionKey = "readiness:order_reference";

        var action = NextActionEndpoints.Pick(issueCase, Now);
        Assert.Equal("schedule_followup", action.Key);
    }

    [Fact]
    public void Dismissed_schedule_followup_key_is_skipped()
    {
        var issueCase = WellPrepared();
        MakeComplete(issueCase);
        issueCase.DismissedNextActionKey = "schedule_followup";

        var action = NextActionEndpoints.Pick(issueCase, Now);
        Assert.Equal("up_to_date", action.Key);
    }
}
