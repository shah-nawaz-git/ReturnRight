import type {
  CaseStatus,
  EvidenceType,
  FinalOutcomeType,
  InteractionType,
  ProblemType,
  RequestedOutcomeType,
} from "./api/types";

export const problemTypeLabels: Record<ProblemType, string> = {
  DamagedItem: "Damaged item",
  DefectiveItem: "Item doesn't work properly",
  WrongItemReceived: "Wrong item received",
  MissingItem: "Missing item",
  DeliveryProblem: "Delivery problem",
  RefundProblem: "Refund problem",
  Other: "Other problem",
};

export const problemTypeOptions: {
  value: ProblemType;
  label: string;
  helper: string;
}[] = [
  { value: "DamagedItem", label: "Damaged item", helper: "It arrived broken, scratched or crushed." },
  { value: "DefectiveItem", label: "Item doesn't work properly", helper: "It stopped working or never worked right." },
  { value: "WrongItemReceived", label: "Wrong item received", helper: "You got something different from what you ordered." },
  { value: "MissingItem", label: "Missing item", helper: "Part of the order never arrived." },
  { value: "DeliveryProblem", label: "Delivery problem", helper: "The parcel is late, lost or delivered somewhere else." },
  { value: "RefundProblem", label: "Refund problem", helper: "You returned something or were promised money back." },
  { value: "Other", label: "Something else", helper: "Another problem with this purchase." },
];

export const outcomeLabels: Record<RequestedOutcomeType, string> = {
  FullRefund: "Full refund",
  PartialRefund: "Partial refund",
  Replacement: "Replacement",
  Repair: "Repair",
  MissingItemDelivered: "Missing item delivered",
  Other: "Other outcome",
};

export const outcomeOptions: {
  value: RequestedOutcomeType;
  label: string;
  helper: string;
}[] = [
  { value: "FullRefund", label: "Full refund", helper: "Get all your money back." },
  { value: "PartialRefund", label: "Partial refund", helper: "Get part of your money back." },
  { value: "Replacement", label: "Replacement", helper: "Get a new item instead." },
  { value: "Repair", label: "Repair", helper: "Get the item fixed." },
  { value: "MissingItemDelivered", label: "Send the missing item", helper: "Get the item you never received." },
  { value: "Other", label: "Something else", helper: "Another resolution." },
];

export const statusLabels: Record<CaseStatus, string> = {
  Open: "Open",
  SellerContacted: "Seller contacted",
  WaitingForSeller: "Waiting for seller",
  ReturnInProgress: "Return in progress",
  RefundPending: "Refund pending",
  ReplacementPending: "Replacement pending",
  Resolved: "Resolved",
  Closed: "Closed",
};

export const activeStatusOptions: CaseStatus[] = [
  "Open",
  "SellerContacted",
  "WaitingForSeller",
  "ReturnInProgress",
  "RefundPending",
  "ReplacementPending",
];

export const finalOutcomeLabels: Record<FinalOutcomeType, string> = {
  FullRefundReceived: "Full refund received",
  PartialRefundReceived: "Partial refund received",
  ReplacementReceived: "Replacement received",
  RepairCompleted: "Repair completed",
  SellerRejected: "Seller rejected the request",
  UserAbandoned: "Stopped pursuing",
  Other: "Other outcome",
};

export const finalOutcomeOptions: {
  value: FinalOutcomeType;
  label: string;
  helper: string;
}[] = [
  { value: "FullRefundReceived", label: "Full refund received", helper: "You got all your money back." },
  { value: "PartialRefundReceived", label: "Partial refund received", helper: "You got some money back." },
  { value: "ReplacementReceived", label: "Replacement received", helper: "A working replacement arrived." },
  { value: "RepairCompleted", label: "Repair completed", helper: "The item was fixed." },
  { value: "SellerRejected", label: "Seller rejected the request", helper: "The seller said no." },
  { value: "UserAbandoned", label: "Stopped pursuing", helper: "You decided not to continue." },
  { value: "Other", label: "Other outcome", helper: "Something else happened." },
];

export const evidenceTypeLabels: Record<EvidenceType, string> = {
  PurchaseProof: "Purchase proof",
  ProductPhoto: "Product photo",
  DamagePhoto: "Damage photo",
  SellerCommunication: "Seller communication",
  DeliveryTracking: "Delivery tracking",
  ReturnConfirmation: "Return confirmation",
  RefundConfirmation: "Refund confirmation",
  Other: "Other evidence",
};

export const evidenceTypeOptions: {
  value: EvidenceType;
  label: string;
  helper: string;
}[] = [
  { value: "DamagePhoto", label: "Damage photo", helper: "Photos showing what's wrong." },
  { value: "ProductPhoto", label: "Product photo", helper: "Photos of the item itself." },
  { value: "PurchaseProof", label: "Purchase proof", helper: "Receipt, invoice or order confirmation." },
  { value: "SellerCommunication", label: "Seller communication", helper: "Screenshots of emails or chats." },
  { value: "DeliveryTracking", label: "Delivery tracking", helper: "Tracking pages or delivery notices." },
  { value: "ReturnConfirmation", label: "Return confirmation", helper: "Proof you sent it back." },
  { value: "RefundConfirmation", label: "Refund confirmation", helper: "Proof a refund was issued." },
  { value: "Other", label: "Other", helper: "Anything else that helps." },
];

export const interactionTypeLabels: Record<InteractionType, string> = {
  ContactedSeller: "Contacted seller",
  SellerResponded: "Seller responded",
  SellerRequestedInformation: "Seller requested information",
  ReturnApproved: "Return approved",
  ReturnShipped: "Return shipped",
  RefundPromised: "Refund promised",
  ReplacementPromised: "Replacement promised",
  SellerRejected: "Seller rejected the request",
  PhoneCall: "Phone call with seller",
  Other: "Update recorded",
};

export const interactionTypeGroups: {
  group: string;
  options: { value: InteractionType; label: string }[];
}[] = [
  {
    group: "You contacted the seller",
    options: [
      { value: "ContactedSeller", label: "I contacted the seller" },
      { value: "PhoneCall", label: "I called the seller" },
      { value: "ReturnShipped", label: "I shipped the return" },
    ],
  },
  {
    group: "Seller replied",
    options: [
      { value: "SellerResponded", label: "Seller responded" },
      { value: "SellerRequestedInformation", label: "Seller asked for information" },
      { value: "RefundPromised", label: "Seller promised a refund" },
      { value: "ReplacementPromised", label: "Seller promised a replacement" },
      { value: "ReturnApproved", label: "Seller approved a return" },
      { value: "SellerRejected", label: "Seller rejected the request" },
    ],
  },
  {
    group: "Other",
    options: [{ value: "Other", label: "Something else happened" }],
  },
];

export const documentCategoryLabels: Record<string, string> = {
  PurchaseDocument: "Purchase document",
  Evidence: "Evidence",
  InteractionAttachment: "Attachment",
};

export const currencyOptions = [
  "EUR",
  "USD",
  "GBP",
  "SEK",
  "NOK",
  "DKK",
  "PLN",
  "CHF",
  "CZK",
  "HUF",
  "CAD",
  "AUD",
  "JPY",
];
