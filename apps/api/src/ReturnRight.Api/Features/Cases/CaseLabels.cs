using ReturnRight.Api.Domain;

namespace ReturnRight.Api.Features.Cases;

public static class CaseLabels
{
    public static string Label(ProblemType type) => type switch
    {
        ProblemType.DamagedItem => "Damaged item",
        ProblemType.DefectiveItem => "Item doesn't work properly",
        ProblemType.WrongItemReceived => "Wrong item received",
        ProblemType.MissingItem => "Missing item",
        ProblemType.DeliveryProblem => "Delivery problem",
        ProblemType.RefundProblem => "Refund problem",
        ProblemType.Other => "Other problem",
        _ => type.ToString(),
    };

    public static string Label(RequestedOutcomeType type) => type switch
    {
        RequestedOutcomeType.FullRefund => "Full refund",
        RequestedOutcomeType.PartialRefund => "Partial refund",
        RequestedOutcomeType.Replacement => "Replacement",
        RequestedOutcomeType.Repair => "Repair",
        RequestedOutcomeType.MissingItemDelivered => "Missing item delivered",
        RequestedOutcomeType.Other => "Other outcome",
        _ => type.ToString(),
    };

    public static string Label(CaseStatus status) => status switch
    {
        CaseStatus.Open => "Open",
        CaseStatus.SellerContacted => "Seller contacted",
        CaseStatus.WaitingForSeller => "Waiting for seller",
        CaseStatus.ReturnInProgress => "Return in progress",
        CaseStatus.RefundPending => "Refund pending",
        CaseStatus.ReplacementPending => "Replacement pending",
        CaseStatus.Resolved => "Resolved",
        CaseStatus.Closed => "Closed",
        _ => status.ToString(),
    };

    public static string Label(FinalOutcomeType type) => type switch
    {
        FinalOutcomeType.FullRefundReceived => "Full refund received",
        FinalOutcomeType.PartialRefundReceived => "Partial refund received",
        FinalOutcomeType.ReplacementReceived => "Replacement received",
        FinalOutcomeType.RepairCompleted => "Repair completed",
        FinalOutcomeType.SellerRejected => "Seller rejected the request",
        FinalOutcomeType.UserAbandoned => "Stopped pursuing",
        FinalOutcomeType.Other => "Other outcome",
        _ => type.ToString(),
    };

    public static string Label(EvidenceType type) => type switch
    {
        EvidenceType.PurchaseProof => "Purchase proof",
        EvidenceType.ProductPhoto => "Product photo",
        EvidenceType.DamagePhoto => "Damage photo",
        EvidenceType.SellerCommunication => "Seller communication",
        EvidenceType.DeliveryTracking => "Delivery tracking",
        EvidenceType.ReturnConfirmation => "Return confirmation",
        EvidenceType.RefundConfirmation => "Refund confirmation",
        EvidenceType.Other => "Other evidence",
        _ => type.ToString(),
    };

    public static string Label(InteractionType type) => type switch
    {
        InteractionType.ContactedSeller => "Contacted seller",
        InteractionType.SellerResponded => "Seller responded",
        InteractionType.SellerRequestedInformation => "Seller requested information",
        InteractionType.ReturnApproved => "Return approved",
        InteractionType.ReturnShipped => "Return shipped",
        InteractionType.RefundPromised => "Refund promised",
        InteractionType.ReplacementPromised => "Replacement promised",
        InteractionType.SellerRejected => "Seller rejected the request",
        InteractionType.PhoneCall => "Phone call with seller",
        InteractionType.Other => "Update recorded",
        _ => type.ToString(),
    };
}
