using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases;

namespace ReturnRight.Api.Features.Interactions;

/// <summary>
/// Pure side-effect rules for recording a seller interaction.
/// Returns the extra timeline events to write; mutates case status/fields
/// in memory. Callers persist. Never touches Resolved/Closed cases.
/// </summary>
public static class InteractionRules
{
    public static IReadOnlyList<(TimelineEventType Type, string Summary)> Apply(
        IssueCase issueCase,
        Interaction interaction,
        DateOnly? expectedBy,
        DateTimeOffset now)
    {
        var events = new List<(TimelineEventType, string)>();
        if (issueCase.Status is CaseStatus.Resolved or CaseStatus.Closed)
        {
            return events;
        }

        switch (interaction.InteractionType)
        {
            case InteractionType.ContactedSeller:
                if (issueCase.Status == CaseStatus.Open)
                {
                    issueCase.Status = CaseStatus.SellerContacted;
                    events.Add((TimelineEventType.StatusChanged, "Status changed to Seller contacted"));
                }
                break;

            case InteractionType.RefundPromised:
                ApplyPromise(
                    issueCase, interaction, expectedBy,
                    CaseStatus.RefundPending, "Seller promised a refund", events);
                break;

            case InteractionType.ReplacementPromised:
                ApplyPromise(
                    issueCase, interaction, expectedBy,
                    CaseStatus.ReplacementPending, "Seller promised a replacement", events);
                break;

            case InteractionType.ReturnApproved or InteractionType.ReturnShipped:
                if (issueCase.Status is CaseStatus.Open
                    or CaseStatus.SellerContacted
                    or CaseStatus.WaitingForSeller)
                {
                    issueCase.Status = CaseStatus.ReturnInProgress;
                    events.Add((TimelineEventType.StatusChanged, "Status changed to Return in progress"));
                }
                break;

            case InteractionType.SellerRequestedInformation:
            case InteractionType.SellerResponded:
            default:
                break;
        }

        return events;
    }

    private static void ApplyPromise(
        IssueCase issueCase,
        Interaction interaction,
        DateOnly? expectedBy,
        CaseStatus pendingStatus,
        string promisedSummary,
        List<(TimelineEventType, string)> events)
    {
        issueCase.OutcomePromisedAt = interaction.OccurredAt;
        if (expectedBy is not null)
        {
            issueCase.OutcomeExpectedBy = expectedBy;
        }

        issueCase.Status = pendingStatus;
        events.Add((TimelineEventType.StatusChanged,
            $"Status changed to {CaseLabels.Label(pendingStatus)}"));

        var summary = expectedBy is null
            ? promisedSummary
            : $"{promisedSummary} by {expectedBy.Value:d MMM yyyy}";
        events.Add((TimelineEventType.OutcomePromised, summary));
    }
}
