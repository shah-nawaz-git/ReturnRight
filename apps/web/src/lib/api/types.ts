// Mirrors the API contracts (apps/api) exactly.

export type ProblemType =
  | "DamagedItem"
  | "DefectiveItem"
  | "WrongItemReceived"
  | "MissingItem"
  | "DeliveryProblem"
  | "RefundProblem"
  | "Other";

export type RequestedOutcomeType =
  | "FullRefund"
  | "PartialRefund"
  | "Replacement"
  | "Repair"
  | "MissingItemDelivered"
  | "Other";

export type CaseStatus =
  | "Open"
  | "SellerContacted"
  | "WaitingForSeller"
  | "ReturnInProgress"
  | "RefundPending"
  | "ReplacementPending"
  | "Resolved"
  | "Closed";

export type FinalOutcomeType =
  | "FullRefundReceived"
  | "PartialRefundReceived"
  | "ReplacementReceived"
  | "RepairCompleted"
  | "SellerRejected"
  | "UserAbandoned"
  | "Other";

export type EvidenceType =
  | "PurchaseProof"
  | "ProductPhoto"
  | "DamagePhoto"
  | "SellerCommunication"
  | "DeliveryTracking"
  | "ReturnConfirmation"
  | "RefundConfirmation"
  | "Other";

export type InteractionType =
  | "ContactedSeller"
  | "SellerResponded"
  | "SellerRequestedInformation"
  | "ReturnApproved"
  | "ReturnShipped"
  | "RefundPromised"
  | "ReplacementPromised"
  | "SellerRejected"
  | "PhoneCall"
  | "Other";

export type ReminderChannel = "Email" | "InApp";
export type ReminderStatus = "Scheduled" | "Sent" | "Failed" | "Cancelled";
export type FieldSource = "UserEntered" | "ExtractedFromDocument" | "Imported";
export type IntakeStatus = "processing" | "succeeded" | "failed";

export interface ProblemDetails {
  type?: string;
  title?: string;
  status?: number;
  detail?: string;
  code?: string;
  errors?: Record<string, string[]>;
}

export interface MeResponse {
  id: string;
  email: string;
  emailRemindersEnabled: boolean;
  inAppRemindersEnabled: boolean;
  unreadNotifications: number;
}

export interface DocumentResponse {
  id: string;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  category: string;
  createdAt: string;
  isImage: boolean;
}

export interface EvidenceResponse {
  id: string;
  evidenceType: EvidenceType;
  description: string | null;
  document: DocumentResponse;
  createdAt: string;
}

export interface InteractionResponse {
  id: string;
  interactionType: InteractionType;
  occurredAt: string;
  note: string;
  document: DocumentResponse | null;
  createdAt: string;
}

export interface FollowUpResponse {
  id: string;
  title: string;
  dueAt: string;
  completedAt: string | null;
  cancelledAt: string | null;
  isOverdue: boolean;
}

export interface ReminderResponse {
  id: string;
  followUpId: string | null;
  channel: ReminderChannel;
  status: ReminderStatus;
  scheduledFor: string;
  nextAttemptAt: string;
  sentAt: string | null;
  attemptCount: number;
  lastError: string | null;
}

export interface TimelineEventResponse {
  id: string;
  eventType: string;
  occurredAt: string;
  summary: string;
}

export interface ReadinessItem {
  key: string;
  label: string;
  isComplete: boolean;
  explanation: string | null;
}

export interface CaseReadinessResponse {
  items: ReadinessItem[];
  completed: number;
  total: number;
}

export type NextActionType =
  | "None"
  | "AddPurchaseProof"
  | "SetRequestedOutcome"
  | "RecordSellerContact"
  | "RecordUpdate"
  | "ChangeFollowUp"
  | "MarkOutcomeReceived"
  | "ScheduleFollowUp"
  | "AddEvidence"
  | "EditCase";

export interface NextActionResponse {
  key: string;
  kind: string;
  title: string;
  description: string;
  actionType: NextActionType;
  followUpId: string | null;
  date: string | null;
  isDismissible: boolean;
}

export interface FinalOutcomeResponse {
  type: FinalOutcomeType;
  amount: number | null;
  currency: string | null;
  on: string | null;
  note: string | null;
}

export interface ProvenanceResponse {
  source: FieldSource;
  confidence: number | null;
  confirmedByUser: boolean;
  sourceDocumentId: string | null;
}

export interface PurchaseItemResponse {
  id: string;
  productName: string;
  quantity: number;
  unitPrice: number | null;
  returnDeadline: string | null;
  returnDeadlineProvenance: ProvenanceResponse;
  commercialWarrantyEnd: string | null;
  commercialWarrantyEndProvenance: ProvenanceResponse;
}

export interface CasePurchaseResponse {
  id: string;
  merchantName: string;
  orderNumber: string | null;
  purchaseDate: string | null;
  currency: string;
  totalAmount: number | null;
  items: PurchaseItemResponse[];
  documents: DocumentResponse[];
}

export interface CaseSummaryResponse {
  id: string;
  title: string;
  problemType: ProblemType;
  status: CaseStatus;
  requestedOutcomeType: RequestedOutcomeType;
  requestedAmount: number | null;
  requestedCurrency: string | null;
  merchantName: string | null;
  purchaseId: string | null;
  createdAt: string;
  updatedAt: string;
  nextFollowUpAt: string | null;
  hasOverdueFollowUp: boolean;
  outcomeExpectedBy: string | null;
}

export interface CaseDetailResponse {
  id: string;
  title: string;
  problemType: ProblemType;
  description: string;
  problemDiscoveredOn: string | null;
  status: CaseStatus;
  requestedOutcomeType: RequestedOutcomeType;
  requestedAmount: number | null;
  requestedCurrency: string | null;
  outcomeRequestedAt: string | null;
  outcomePromisedAt: string | null;
  outcomeExpectedBy: string | null;
  outcomeCompletedAt: string | null;
  finalOutcome: FinalOutcomeResponse | null;
  resolvedAt: string | null;
  purchase: CasePurchaseResponse | null;
  affectedItemIds: string[];
  evidence: EvidenceResponse[];
  interactions: InteractionResponse[];
  followUps: FollowUpResponse[];
  timeline: TimelineEventResponse[];
  readiness: CaseReadinessResponse;
  nextAction: NextActionResponse;
  createdAt: string;
  updatedAt: string;
}

export interface UpcomingFollowUpResponse {
  followUpId: string;
  caseId: string;
  caseTitle: string;
  title: string;
  dueAt: string;
}

export interface HomeResponse {
  activeCaseCount: number;
  followUpsDueTodayCount: number;
  overdueFollowUpCount: number;
  attention: CaseSummaryResponse[];
  upcomingFollowUps: UpcomingFollowUpResponse[];
  recentCases: CaseSummaryResponse[];
}

export interface PurchaseSummaryResponse {
  id: string;
  merchantName: string;
  orderNumber: string | null;
  purchaseDate: string | null;
  currency: string;
  totalAmount: number | null;
  itemCount: number;
  firstItemName: string | null;
  hasProof: boolean;
  activeCaseCount: number;
  createdAt: string;
}

export interface PurchaseCaseSummary {
  id: string;
  problemType: ProblemType;
  status: CaseStatus;
  createdAt: string;
}

export interface PurchaseProvenanceResponse {
  merchantName: ProvenanceResponse;
  orderNumber: ProvenanceResponse;
  purchaseDate: ProvenanceResponse;
  totalAmount: ProvenanceResponse;
}

export interface PurchaseDetailResponse {
  id: string;
  merchantName: string;
  orderNumber: string | null;
  purchaseDate: string | null;
  currency: string;
  totalAmount: number | null;
  notes: string | null;
  provenance: PurchaseProvenanceResponse;
  items: PurchaseItemResponse[];
  documents: DocumentResponse[];
  cases: PurchaseCaseSummary[];
  createdAt: string;
  updatedAt: string;
}

export interface FieldCandidate {
  field: string;
  value: string;
  confidence: number;
  evidence: string | null;
}

export interface ItemCandidate {
  name: string;
  quantity: number | null;
  unitPrice: number | null;
  confidence: number;
}

export interface IntakeResponse {
  id: string;
  status: IntakeStatus;
  fileName: string;
  contentType: string;
  sizeBytes: number;
  expiresAt: string;
  usedOcr: boolean;
  candidates: FieldCandidate[];
  items: ItemCandidate[];
  message: string | null;
}

export interface NotificationResponse {
  id: string;
  title: string;
  body: string;
  caseId: string | null;
  createdAt: string;
  readAt: string | null;
}

export interface PurchaseItemInput {
  id?: string | null;
  productName: string;
  quantity: number;
  unitPrice?: number | null;
  returnDeadline?: string | null;
  commercialWarrantyEnd?: string | null;
  returnDeadlineSource?: FieldSource;
  commercialWarrantyEndSource?: FieldSource;
}

export interface CreateCaseRequest {
  problemType: ProblemType;
  description: string;
  requestedOutcomeType: RequestedOutcomeType;
  requestedAmount?: number | null;
  requestedCurrency?: string | null;
  purchaseId?: string | null;
  affectedItemIds?: string[] | null;
  problemDiscoveredOn?: string | null;
}

export interface CreateInteractionRequest {
  interactionType: InteractionType;
  occurredAt: string;
  note: string;
  expectedBy?: string | null;
  promisedAmount?: number | null;
}
