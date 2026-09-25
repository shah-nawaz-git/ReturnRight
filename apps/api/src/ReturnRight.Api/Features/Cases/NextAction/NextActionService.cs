using ReturnRight.Api.Domain;
using ReturnRight.Api.Features.Cases.Readiness;

namespace ReturnRight.Api.Features.Cases.NextAction;

public record NextActionResponse(
    string Key,
    string Kind,
    string Title,
    string Description,
    string ActionType,
    Guid? FollowUpId,
    DateTimeOffset? Date,
    bool IsDismissible);

/// <summary>
/// Pure priority-ordered "what should I do next" candidates.
/// kind = the rule that produced the candidate; key = unique instance
/// (dynamic keys carry the follow-up id). Callers pick the first candidate
/// whose key doesn't match the dismissed one.
/// </summary>
public static class NextActionService
{
    public static List<NextActionResponse> Evaluate(
        IssueCase issueCase, CaseReadinessResponse readiness, DateTimeOffset now)
    {
        var candidates = new List<NextActionResponse>();

        // 1. Finished cases stop here.
        if (issueCase.Status is CaseStatus.Resolved or CaseStatus.Closed)
        {
            var resolved = issueCase.Status == CaseStatus.Resolved;
            var outcome = issueCase.FinalOutcomeType is { } final
                ? $"{CaseLabels.Label(final)}"
                    + (issueCase.FinalAmount is { } amount
                        ? $" — {amount:0.##} {issueCase.FinalCurrency}"
                        : string.Empty)
                    + (issueCase.FinalOutcomeOn is { } on ? $" on {on:d MMM yyyy}" : string.Empty)
                    + "."
                : "No final outcome recorded.";
            candidates.Add(new(
                "resolved",
                "resolved",
                resolved ? "This case is resolved" : "This case is closed",
                outcome,
                "None",
                null,
                null,
                IsDismissible: false));
            return candidates;
        }

        var item = new Func<string, ReadinessItem?>(key =>
            readiness.Items.FirstOrDefault(i => i.Key == key));

        // 2–4. Readiness-driven basics.
        if (item("purchase_proof") is { IsComplete: false })
        {
            candidates.Add(new(
                "add_proof", "add_proof",
                "Add your purchase proof",
                "A receipt, invoice or order confirmation shows what you bought and when.",
                "AddPurchaseProof", null, null, IsDismissible: false));
        }
        if (item("requested_outcome") is { IsComplete: false })
        {
            candidates.Add(new(
                "set_outcome", "set_outcome",
                "Record what you're asking for",
                "Add the amount you want refunded so it's clear what you expect.",
                "SetRequestedOutcome", null, null, IsDismissible: false));
        }
        if (issueCase.Interactions.Count == 0)
        {
            candidates.Add(new(
                "contact_seller", "contact_seller",
                "Tell the seller about the problem",
                "Record when you contacted the seller and what you asked for.",
                "RecordSellerContact", null, null, IsDismissible: false));
        }

        var openFollowUps = issueCase.FollowUps
            .Where(f => f.CompletedAt is null && f.CancelledAt is null)
            .OrderBy(f => f.DueAt)
            .ToList();

        // 5. Overdue follow-up.
        if (openFollowUps.FirstOrDefault(f => f.DueAt < now) is { } overdue)
        {
            candidates.Add(new(
                $"followup_overdue:{overdue.Id}", "followup_overdue",
                "Follow-up due",
                $"Your follow-up '{overdue.Title}' was due {overdue.DueAt:d MMM}. "
                    + "Record what happened or move it.",
                "RecordUpdate", overdue.Id, overdue.DueAt, IsDismissible: false));
        }

        // 6. Expected outcome date passed.
        var today = DateOnly.FromDateTime(now.DateTime);
        if (issueCase.OutcomeExpectedBy is { } expectedBy
            && expectedBy < today
            && issueCase.OutcomeCompletedAt is null)
        {
            var word = issueCase.RequestedOutcomeType switch
            {
                RequestedOutcomeType.FullRefund or RequestedOutcomeType.PartialRefund => "refund",
                RequestedOutcomeType.Replacement => "replacement",
                RequestedOutcomeType.Repair => "repair",
                RequestedOutcomeType.MissingItemDelivered => "missing item",
                _ => "outcome",
            };
            candidates.Add(new(
                "outcome_expected_passed", "outcome_expected_passed",
                $"Check whether your {word} arrived",
                $"The seller's expected date ({expectedBy:d MMM yyyy}) has passed. "
                    + "Mark it received or schedule a follow-up.",
                "MarkOutcomeReceived", null, null, IsDismissible: false));
        }

        // 7. Waiting on the seller with a scheduled follow-up.
        if (issueCase.Status is CaseStatus.SellerContacted or CaseStatus.WaitingForSeller
                or CaseStatus.ReturnInProgress or CaseStatus.RefundPending
                or CaseStatus.ReplacementPending
            && openFollowUps.FirstOrDefault(f => f.DueAt >= now) is { } waiting)
        {
            var lastUpdate = issueCase.TimelineEvents
                .Select(e => (DateTimeOffset?)e.OccurredAt)
                .Max() ?? issueCase.UpdatedAt;
            candidates.Add(new(
                $"waiting:{waiting.Id}", "waiting",
                issueCase.Status switch
                {
                    CaseStatus.ReturnInProgress => "Return in progress",
                    CaseStatus.RefundPending => "Refund pending",
                    CaseStatus.ReplacementPending => "Replacement pending",
                    _ => "Waiting for seller response",
                },
                $"You last updated this case on {lastUpdate:d MMM}. "
                    + $"Follow up on {waiting.DueAt:d MMM}.",
                "ChangeFollowUp", waiting.Id, waiting.DueAt, IsDismissible: false));
        }

        // 8. First other incomplete readiness item.
        var covered = new HashSet<string> { "purchase_proof", "requested_outcome", "seller_contact" };
        var incomplete = readiness.Items
            .FirstOrDefault(i => !i.IsComplete && !covered.Contains(i.Key));
        if (incomplete is not null)
        {
            var actionType = incomplete.Key switch
            {
                "supporting_evidence" => "AddEvidence",
                "refund_details" => "RecordUpdate",
                _ => "EditCase",
            };
            candidates.Add(new(
                $"readiness:{incomplete.Key}", "readiness",
                $"Add {incomplete.Label.ToLowerInvariant()}",
                incomplete.Explanation ?? "Complete this checklist item.",
                actionType, null, null, IsDismissible: true));
        }

        // 9. Nothing scheduled yet — suggest booking a check-in.
        if (openFollowUps.Count == 0 && issueCase.Interactions.Count > 0)
        {
            candidates.Add(new(
                "schedule_followup", "schedule_followup",
                "Schedule a follow-up",
                "Pick a date to check back with the seller so this doesn't stall.",
                "ScheduleFollowUp", null, null, IsDismissible: true));
        }

        // 10. Fallback.
        candidates.Add(new(
            "up_to_date", "up_to_date",
            "You're up to date",
            "Update this case when the seller replies or something changes.",
            "RecordUpdate", null, null, IsDismissible: false));

        return candidates;
    }
}
