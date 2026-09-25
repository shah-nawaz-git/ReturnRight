using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Interactions;

namespace ReturnRight.UnitTests;

public class InteractionRulesTests
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);

    private static IssueCase Case(CaseStatus status = CaseStatus.Open) => new()
    {
        UserId = Guid.NewGuid(),
        ProblemType = ProblemType.DamagedItem,
        Description = "Arrived broken.",
        RequestedOutcomeType = RequestedOutcomeType.FullRefund,
        Status = status,
    };

    private static Interaction Interaction(
        InteractionType type, DateTimeOffset? occurredAt = null) => new()
        {
            InteractionType = type,
            OccurredAt = occurredAt ?? Now,
            Note = "note",
        };

    [Fact]
    public void Contacted_seller_on_open_moves_to_seller_contacted()
    {
        var issueCase = Case();
        var events = InteractionRules.Apply(
            issueCase, Interaction(InteractionType.ContactedSeller), null, Now);

        Assert.Equal(CaseStatus.SellerContacted, issueCase.Status);
        var evt = Assert.Single(events);
        Assert.Equal(TimelineEventType.StatusChanged, evt.Item1);
    }

    [Fact]
    public void Contacted_seller_when_already_waiting_keeps_status()
    {
        var issueCase = Case(CaseStatus.WaitingForSeller);
        var events = InteractionRules.Apply(
            issueCase, Interaction(InteractionType.ContactedSeller), null, Now);

        Assert.Equal(CaseStatus.WaitingForSeller, issueCase.Status);
        Assert.Empty(events);
    }

    [Fact]
    public void Refund_promised_sets_outcome_fields_status_and_events()
    {
        var issueCase = Case(CaseStatus.SellerContacted);
        var occurred = Now.AddDays(-1);
        var expectedBy = new DateOnly(2026, 10, 15);

        var events = InteractionRules.Apply(
            issueCase, Interaction(InteractionType.RefundPromised, occurred), expectedBy, Now);

        Assert.Equal(occurred, issueCase.OutcomePromisedAt);
        Assert.Equal(expectedBy, issueCase.OutcomeExpectedBy);
        Assert.Equal(CaseStatus.RefundPending, issueCase.Status);
        Assert.Equal(2, events.Count);
        Assert.Equal(TimelineEventType.StatusChanged, events[0].Item1);
        Assert.Equal(TimelineEventType.OutcomePromised, events[1].Item1);
        Assert.Equal("Seller promised a refund by 15 Oct 2026", events[1].Item2);
    }

    [Fact]
    public void Refund_promised_without_expected_date_omits_it()
    {
        var issueCase = Case();
        var events = InteractionRules.Apply(
            issueCase, Interaction(InteractionType.RefundPromised), null, Now);

        Assert.Null(issueCase.OutcomeExpectedBy);
        Assert.Equal("Seller promised a refund", events[1].Item2);
    }

    [Fact]
    public void Replacement_promised_sets_replacement_pending()
    {
        var issueCase = Case(CaseStatus.SellerContacted);
        var expectedBy = new DateOnly(2026, 11, 1);

        var events = InteractionRules.Apply(
            issueCase, Interaction(InteractionType.ReplacementPromised), expectedBy, Now);

        Assert.Equal(CaseStatus.ReplacementPending, issueCase.Status);
        Assert.Equal("Seller promised a replacement by 1 Nov 2026", events[1].Item2);
    }

    [Theory]
    [InlineData(CaseStatus.Open)]
    [InlineData(CaseStatus.SellerContacted)]
    [InlineData(CaseStatus.WaitingForSeller)]
    public void Return_progress_moves_from_early_statuses(CaseStatus status)
    {
        var issueCase = Case(status);
        var events = InteractionRules.Apply(
            issueCase, Interaction(InteractionType.ReturnApproved), null, Now);

        Assert.Equal(CaseStatus.ReturnInProgress, issueCase.Status);
        Assert.Single(events);
        Assert.Equal(TimelineEventType.StatusChanged, events[0].Item1);
    }

    [Fact]
    public void Return_shipped_does_not_regress_refund_pending()
    {
        var issueCase = Case(CaseStatus.RefundPending);
        var events = InteractionRules.Apply(
            issueCase, Interaction(InteractionType.ReturnShipped), null, Now);

        Assert.Equal(CaseStatus.RefundPending, issueCase.Status);
        Assert.Empty(events);
    }

    [Theory]
    [InlineData(InteractionType.SellerRequestedInformation)]
    [InlineData(InteractionType.SellerResponded)]
    [InlineData(InteractionType.SellerRejected)]
    public void Informational_types_never_change_status(InteractionType type)
    {
        var issueCase = Case(CaseStatus.WaitingForSeller);
        var events = InteractionRules.Apply(issueCase, Interaction(type), null, Now);

        Assert.Equal(CaseStatus.WaitingForSeller, issueCase.Status);
        Assert.Empty(events);
    }

    [Theory]
    [InlineData(InteractionType.ContactedSeller)]
    [InlineData(InteractionType.RefundPromised)]
    [InlineData(InteractionType.ReturnApproved)]
    public void Resolved_case_status_is_untouched(InteractionType type)
    {
        var issueCase = Case(CaseStatus.Resolved);
        var events = InteractionRules.Apply(
            issueCase, Interaction(type), new DateOnly(2026, 10, 20), Now);

        Assert.Equal(CaseStatus.Resolved, issueCase.Status);
        Assert.Null(issueCase.OutcomePromisedAt);
        Assert.Empty(events);
    }
}
