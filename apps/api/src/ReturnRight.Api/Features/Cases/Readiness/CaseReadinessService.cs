using ReturnRight.Api.Domain;

namespace ReturnRight.Api.Features.Cases.Readiness;

public record ReadinessItem(string Key, string Label, bool IsComplete, string? Explanation);

public record CaseReadinessResponse(ReadinessItem[] Items, int Completed, int Total);

/// <summary>
/// Pure checklist over a fully-loaded case graph — what the user still needs
/// to add before their case is well-prepared.
/// </summary>
public static class CaseReadinessService
{
    public static CaseReadinessResponse Evaluate(IssueCase issueCase)
    {
        var items = new List<ReadinessItem>();

        var hasProof = issueCase.Evidence.Any(e => e.EvidenceType == EvidenceType.PurchaseProof)
            || issueCase.Purchase?.Documents.Count > 0;
        items.Add(new(
            "purchase_proof",
            "Purchase proof",
            hasProof,
            hasProof ? null : "Add a receipt, invoice or order confirmation."));

        if (issueCase.ProblemType is not (ProblemType.RefundProblem or ProblemType.Other))
        {
            var hasOrder = !string.IsNullOrWhiteSpace(issueCase.Purchase?.OrderNumber);
            items.Add(new(
                "order_reference",
                "Order or reference number",
                hasOrder,
                hasOrder ? null : "Add the order number from your confirmation or receipt."));
        }

        var described = issueCase.Description.Trim().Length >= 20;
        items.Add(new(
            "problem_description",
            "Problem description",
            described,
            described ? null : "Describe what happened in a couple of sentences."));

        var outcomeSet = issueCase.RequestedOutcomeType
                is not (RequestedOutcomeType.FullRefund or RequestedOutcomeType.PartialRefund)
            || issueCase.RequestedAmount is not null;
        items.Add(new(
            "requested_outcome",
            "Requested outcome",
            outcomeSet,
            outcomeSet ? null : "Add the amount you're asking the seller to refund."));

        items.Add(SupportingEvidence(issueCase));

        var contacted = issueCase.Interactions.Count > 0;
        items.Add(new(
            "seller_contact",
            "Seller contacted",
            contacted,
            contacted ? null : "Record when and how you contacted the seller."));

        if (issueCase.ProblemType == ProblemType.RefundProblem)
        {
            var hasRefundDetails = issueCase.OutcomePromisedAt is not null
                || issueCase.OutcomeExpectedBy is not null;
            items.Add(new(
                "refund_details",
                "Promised or expected refund date",
                hasRefundDetails,
                hasRefundDetails ? null : "Record when the seller promised the refund."));
        }

        return new CaseReadinessResponse(
            items.ToArray(), items.Count(i => i.IsComplete), items.Count);
    }

    private static ReadinessItem SupportingEvidence(IssueCase issueCase) =>
        issueCase.ProblemType switch
        {
            ProblemType.DamagedItem or ProblemType.DefectiveItem or ProblemType.WrongItemReceived =>
                Check(issueCase, "Photos of the item",
                    "Add photos that show the problem.",
                    e => e.EvidenceType is EvidenceType.DamagePhoto or EvidenceType.ProductPhoto),
            ProblemType.MissingItem or ProblemType.DeliveryProblem =>
                Check(issueCase, "Delivery or tracking evidence",
                    "Add tracking info or a delivery notice if you have one.",
                    e => e.EvidenceType is EvidenceType.DeliveryTracking
                        or EvidenceType.SellerCommunication),
            ProblemType.RefundProblem =>
                Check(issueCase, "Return or refund confirmation",
                    "Add the return or refund confirmation if you received one.",
                    e => e.EvidenceType is EvidenceType.ReturnConfirmation
                        or EvidenceType.RefundConfirmation),
            _ =>
                Check(issueCase, "Supporting evidence",
                    "Add anything that helps explain the problem.",
                    e => e.EvidenceType != EvidenceType.PurchaseProof),
        };

    private static ReadinessItem Check(
        IssueCase issueCase, string label, string explanation, Func<Domain.Evidence, bool> predicate)
    {
        var key = "supporting_evidence";
        var complete = issueCase.Evidence.Any(predicate);
        return new ReadinessItem(key, label, complete, complete ? null : explanation);
    }
}
