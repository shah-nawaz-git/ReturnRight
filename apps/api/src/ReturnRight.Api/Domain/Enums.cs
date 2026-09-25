namespace ReturnRight.Api.Domain;

public enum FieldSource
{
    UserEntered,
    ExtractedFromDocument,
    Imported,
}

public enum DocumentCategory
{
    PurchaseDocument,
    Evidence,
    InteractionAttachment,
}

public enum IntakeStatus
{
    Processing,
    Succeeded,
    Failed,
}

public enum ProblemType
{
    DamagedItem,
    DefectiveItem,
    WrongItemReceived,
    MissingItem,
    DeliveryProblem,
    RefundProblem,
    Other,
}

public enum RequestedOutcomeType
{
    FullRefund,
    PartialRefund,
    Replacement,
    Repair,
    MissingItemDelivered,
    Other,
}

public enum CaseStatus
{
    Open,
    SellerContacted,
    WaitingForSeller,
    ReturnInProgress,
    RefundPending,
    ReplacementPending,
    Resolved,
    Closed,
}

public enum FinalOutcomeType
{
    FullRefundReceived,
    PartialRefundReceived,
    ReplacementReceived,
    RepairCompleted,
    SellerRejected,
    UserAbandoned,
    Other,
}

public enum EvidenceType
{
    PurchaseProof,
    ProductPhoto,
    DamagePhoto,
    SellerCommunication,
    DeliveryTracking,
    ReturnConfirmation,
    RefundConfirmation,
    Other,
}

public enum InteractionType
{
    ContactedSeller,
    SellerResponded,
    SellerRequestedInformation,
    ReturnApproved,
    ReturnShipped,
    RefundPromised,
    ReplacementPromised,
    SellerRejected,
    PhoneCall,
    Other,
}

public enum ReminderChannel
{
    Email,
    InApp,
}

public enum ReminderStatus
{
    Scheduled,
    Sent,
    Failed,
    Cancelled,
}

public enum TimelineEventType
{
    CaseCreated,
    PurchaseLinked,
    StatusChanged,
    RequestedOutcomeChanged,
    EvidenceAdded,
    EvidenceRemoved,
    InteractionAdded,
    FollowUpCreated,
    FollowUpRescheduled,
    FollowUpCompleted,
    FollowUpCancelled,
    OutcomePromised,
    OutcomeCompleted,
    CaseResolved,
    CaseReopened,
}
