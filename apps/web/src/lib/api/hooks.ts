"use client";

import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";

import { apiFetch, uploadFile } from "./client";
import type {
  CaseDetailResponse,
  CaseStatus,
  CaseSummaryResponse,
  CreateCaseRequest,
  CreateInteractionRequest,
  EvidenceResponse,
  EvidenceType,
  FinalOutcomeType,
  FollowUpResponse,
  HomeResponse,
  IntakeResponse,
  InteractionResponse,
  InteractionType,
  MeResponse,
  NotificationResponse,
  ProblemType,
  PurchaseDetailResponse,
  PurchaseItemInput,
  PurchaseSummaryResponse,
  ReminderResponse,
  RequestedOutcomeType,
} from "./types";

export const keys = {
  me: ["me"] as const,
  home: ["home"] as const,
  notifications: ["notifications"] as const,
  cases: (filter?: string) => ["cases", filter ?? "active"] as const,
  case: (id: string) => ["case", id] as const,
  reminders: (id: string) => ["case", id, "reminders"] as const,
  purchases: ["purchases"] as const,
  purchase: (id: string) => ["purchase", id] as const,
  intake: (id: string) => ["intake", id] as const,
};

// ---------------------------------------------------------------------------
// Queries

export function useMe() {
  return useQuery({
    queryKey: keys.me,
    queryFn: () => apiFetch<MeResponse>("/api/auth/me"),
    staleTime: 30_000,
  });
}

export function useHome() {
  return useQuery({ queryKey: keys.home, queryFn: () => apiFetch<HomeResponse>("/api/home") });
}

export function useNotifications() {
  return useQuery({
    queryKey: keys.notifications,
    queryFn: () => apiFetch<NotificationResponse[]>("/api/notifications"),
  });
}

export function useCases(filter: "active" | "resolved" | "all" = "active") {
  return useQuery({
    queryKey: keys.cases(filter),
    queryFn: () => apiFetch<CaseSummaryResponse[]>(`/api/cases?filter=${filter}`),
  });
}

export function useCase(id: string) {
  return useQuery({
    queryKey: keys.case(id),
    queryFn: () => apiFetch<CaseDetailResponse>(`/api/cases/${id}`),
    retry: (count, error) => {
      if ((error as { status?: number }).status === 404) return false;
      return count < 2;
    },
  });
}

export function useCaseReminders(id: string) {
  return useQuery({
    queryKey: keys.reminders(id),
    queryFn: () => apiFetch<ReminderResponse[]>(`/api/cases/${id}/reminders`),
  });
}

export function usePurchases() {
  return useQuery({
    queryKey: keys.purchases,
    queryFn: () => apiFetch<PurchaseSummaryResponse[]>("/api/purchases"),
  });
}

export function usePurchase(id: string, enabled = true) {
  return useQuery({
    queryKey: keys.purchase(id),
    queryFn: () => apiFetch<PurchaseDetailResponse>(`/api/purchases/${id}`),
    enabled: enabled && id !== "",
    retry: (count, error) => {
      if ((error as { status?: number }).status === 404) return false;
      return count < 2;
    },
  });
}

export function useIntake(id: string | null) {
  return useQuery({
    queryKey: keys.intake(id ?? ""),
    queryFn: () => apiFetch<IntakeResponse>(`/api/intakes/${id}`),
    enabled: id !== null,
    refetchInterval: (query) =>
      query.state.data?.status === "processing" ? 1500 : false,
  });
}

// ---------------------------------------------------------------------------
// Invalidation helpers

function useInvalidate() {
  const qc = useQueryClient();
  return {
    me: () => qc.invalidateQueries({ queryKey: keys.me }),
    home: () => qc.invalidateQueries({ queryKey: keys.home }),
    notifications: () => {
      qc.invalidateQueries({ queryKey: keys.notifications });
      qc.invalidateQueries({ queryKey: keys.me });
    },
    cases: () => {
      qc.invalidateQueries({ queryKey: ["cases"] });
      qc.invalidateQueries({ queryKey: keys.home });
    },
    case: (id: string) => {
      qc.invalidateQueries({ queryKey: keys.case(id) });
      qc.invalidateQueries({ queryKey: ["cases"] });
      qc.invalidateQueries({ queryKey: keys.home });
    },
    reminders: (id: string) => qc.invalidateQueries({ queryKey: keys.reminders(id) }),
    purchases: () => {
      qc.invalidateQueries({ queryKey: keys.purchases });
      qc.invalidateQueries({ queryKey: keys.home });
    },
    purchase: (id: string) => {
      qc.invalidateQueries({ queryKey: keys.purchase(id) });
      qc.invalidateQueries({ queryKey: keys.purchases });
    },
  };
}

// ---------------------------------------------------------------------------
// Auth

export function useLogin() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { email: string; password: string }) =>
      apiFetch("/api/auth/login", { method: "POST", body: JSON.stringify(input), headers: { "Content-Type": "application/json" } }),
    onSuccess: () => {
      inv.me();
      inv.home();
    },
  });
}

export function useRegister() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { email: string; password: string }) =>
      apiFetch("/api/auth/register", { method: "POST", body: JSON.stringify(input), headers: { "Content-Type": "application/json" } }),
    onSuccess: () => {
      inv.me();
      inv.home();
    },
  });
}

export function useLogout() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: () => apiFetch("/api/auth/logout", { method: "POST" }),
    onSuccess: () => qc.clear(),
  });
}

export function useUpdateProfile() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { emailRemindersEnabled?: boolean; inAppRemindersEnabled?: boolean }) =>
      apiFetch("/api/profile", { method: "PATCH", body: JSON.stringify(input), headers: { "Content-Type": "application/json" } }),
    onSuccess: () => inv.me(),
  });
}

export function useDeleteAccount() {
  return useMutation({
    mutationFn: (input: { password: string }) =>
      apiFetch("/api/profile", {
        method: "DELETE",
        body: JSON.stringify(input),
        headers: { "Content-Type": "application/json" },
      }),
  });
}

// ---------------------------------------------------------------------------
// Notifications

export function useMarkNotificationRead() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (id: string) => apiFetch(`/api/notifications/${id}/read`, { method: "POST" }),
    onSuccess: () => inv.notifications(),
  });
}

export function useMarkAllNotificationsRead() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: () => apiFetch("/api/notifications/read-all", { method: "POST" }),
    onSuccess: () => inv.notifications(),
  });
}

export function useDeleteNotification() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (id: string) => apiFetch(`/api/notifications/${id}`, { method: "DELETE" }),
    onSuccess: () => inv.notifications(),
  });
}

// ---------------------------------------------------------------------------
// Cases

const json = { "Content-Type": "application/json" };

export function useCreateCase() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: CreateCaseRequest) =>
      apiFetch<CaseDetailResponse>("/api/cases", { method: "POST", body: JSON.stringify(input), headers: json }),
    onSuccess: () => inv.cases(),
  });
}

export function useUpdateCase(id: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: {
      problemType: ProblemType;
      description: string;
      requestedOutcomeType: RequestedOutcomeType;
      requestedAmount?: number | null;
      requestedCurrency?: string | null;
      problemDiscoveredOn?: string | null;
    }) =>
      apiFetch(`/api/cases/${id}`, { method: "PATCH", body: JSON.stringify(input), headers: json }),
    onSuccess: () => inv.case(id),
  });
}

export function useChangeCaseStatus(id: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { status: CaseStatus; note?: string | null }) =>
      apiFetch(`/api/cases/${id}/status`, { method: "POST", body: JSON.stringify(input), headers: json }),
    onSuccess: () => inv.case(id),
  });
}

export function useResolveCase(id: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: {
      finalOutcomeType: FinalOutcomeType;
      finalAmount?: number | null;
      finalCurrency?: string | null;
      completedOn?: string | null;
      note?: string | null;
    }) =>
      apiFetch(`/api/cases/${id}/resolve`, { method: "POST", body: JSON.stringify(input), headers: json }),
    onSuccess: () => inv.case(id),
  });
}

export function useReopenCase(id: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: () => apiFetch(`/api/cases/${id}/reopen`, { method: "POST" }),
    onSuccess: () => inv.case(id),
  });
}

export function useDeleteCase(id: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: () => apiFetch(`/api/cases/${id}`, { method: "DELETE" }),
    onSuccess: () => inv.cases(),
  });
}

export function useDismissNextAction(id: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (key: string) =>
      apiFetch(`/api/cases/${id}/next-action/dismiss`, {
        method: "POST",
        body: JSON.stringify({ key }),
        headers: json,
      }),
    onSuccess: () => inv.case(id),
  });
}

// ---------------------------------------------------------------------------
// Evidence

export function useUploadEvidence(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: {
      file: File;
      evidenceType: EvidenceType;
      description?: string;
      onProgress?: (f: number) => void;
    }) => {
      const fd = new FormData();
      fd.append("file", input.file);
      fd.append("evidenceType", input.evidenceType);
      if (input.description) fd.append("description", input.description);
      return uploadFile<EvidenceResponse>(`/api/cases/${caseId}/evidence`, fd, input.onProgress);
    },
    onSuccess: () => inv.case(caseId),
  });
}

export function useUpdateEvidence(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { evidenceId: string; evidenceType: EvidenceType; description?: string | null }) =>
      apiFetch(`/api/cases/${caseId}/evidence/${input.evidenceId}`, {
        method: "PATCH",
        body: JSON.stringify({ evidenceType: input.evidenceType, description: input.description }),
        headers: json,
      }),
    onSuccess: () => inv.case(caseId),
  });
}

export function useDeleteEvidence(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (evidenceId: string) =>
      apiFetch(`/api/cases/${caseId}/evidence/${evidenceId}`, { method: "DELETE" }),
    onSuccess: () => inv.case(caseId),
  });
}

// ---------------------------------------------------------------------------
// Interactions

export function useCreateInteraction(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: CreateInteractionRequest) =>
      apiFetch<InteractionResponse>(`/api/cases/${caseId}/interactions`, {
        method: "POST",
        body: JSON.stringify(input),
        headers: json,
      }),
    onSuccess: () => inv.case(caseId),
  });
}

export function useUpdateInteraction(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: {
      interactionId: string;
      interactionType: InteractionType;
      occurredAt: string;
      note: string;
    }) =>
      apiFetch(`/api/cases/${caseId}/interactions/${input.interactionId}`, {
        method: "PATCH",
        body: JSON.stringify({
          interactionType: input.interactionType,
          occurredAt: input.occurredAt,
          note: input.note,
        }),
        headers: json,
      }),
    onSuccess: () => inv.case(caseId),
  });
}

export function useDeleteInteraction(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (interactionId: string) =>
      apiFetch(`/api/cases/${caseId}/interactions/${interactionId}`, { method: "DELETE" }),
    onSuccess: () => inv.case(caseId),
  });
}

export function useUploadInteractionAttachment(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { interactionId: string; file: File; onProgress?: (f: number) => void }) => {
      const fd = new FormData();
      fd.append("file", input.file);
      return uploadFile<InteractionResponse>(
        `/api/cases/${caseId}/interactions/${input.interactionId}/attachment`,
        fd,
        input.onProgress,
      );
    },
    onSuccess: () => inv.case(caseId),
  });
}

export function useDeleteInteractionAttachment(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (interactionId: string) =>
      apiFetch(`/api/cases/${caseId}/interactions/${interactionId}/attachment`, { method: "DELETE" }),
    onSuccess: () => inv.case(caseId),
  });
}

// ---------------------------------------------------------------------------
// Follow-ups

export function useCreateFollowUp(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { title: string; dueAt: string }) =>
      apiFetch<FollowUpResponse>(`/api/cases/${caseId}/follow-ups`, {
        method: "POST",
        body: JSON.stringify(input),
        headers: json,
      }),
    onSuccess: () => {
      inv.case(caseId);
      inv.reminders(caseId);
    },
  });
}

export function useUpdateFollowUp(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { followUpId: string; title?: string; dueAt?: string }) =>
      apiFetch(`/api/cases/${caseId}/follow-ups/${input.followUpId}`, {
        method: "PATCH",
        body: JSON.stringify({ title: input.title, dueAt: input.dueAt }),
        headers: json,
      }),
    onSuccess: () => {
      inv.case(caseId);
      inv.reminders(caseId);
    },
  });
}

export function useCompleteFollowUp(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (followUpId: string) =>
      apiFetch(`/api/cases/${caseId}/follow-ups/${followUpId}/complete`, { method: "POST" }),
    onSuccess: () => {
      inv.case(caseId);
      inv.reminders(caseId);
    },
  });
}

export function useCancelFollowUp(caseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (followUpId: string) =>
      apiFetch(`/api/cases/${caseId}/follow-ups/${followUpId}/cancel`, { method: "POST" }),
    onSuccess: () => {
      inv.case(caseId);
      inv.reminders(caseId);
    },
  });
}

// ---------------------------------------------------------------------------
// Purchases + intakes + documents

export interface PurchaseFormPayload {
  merchantName: string;
  orderNumber?: string | null;
  purchaseDate?: string | null;
  currency: string;
  totalAmount?: number | null;
  notes?: string | null;
  items: PurchaseItemInput[];
}

export function useCreatePurchase() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: PurchaseFormPayload) =>
      apiFetch<PurchaseDetailResponse>("/api/purchases", {
        method: "POST",
        body: JSON.stringify(input),
        headers: json,
      }),
    onSuccess: () => inv.purchases(),
  });
}

export function useUpdatePurchase(id: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: PurchaseFormPayload) =>
      apiFetch(`/api/purchases/${id}`, { method: "PATCH", body: JSON.stringify(input), headers: json }),
    onSuccess: () => inv.purchase(id),
  });
}

export function useDeletePurchase(id: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: () => apiFetch(`/api/purchases/${id}`, { method: "DELETE" }),
    onSuccess: () => inv.purchases(),
  });
}

export function useUploadPurchaseProof(purchaseId: string) {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { file: File; onProgress?: (f: number) => void }) => {
      const fd = new FormData();
      fd.append("file", input.file);
      return uploadFile(`/api/purchases/${purchaseId}/documents`, fd, input.onProgress);
    },
    onSuccess: () => inv.purchase(purchaseId),
  });
}

export function useDeleteDocument() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => apiFetch(`/api/documents/${id}`, { method: "DELETE" }),
    onSuccess: () => qc.invalidateQueries(),
  });
}

export function useCreateIntake() {
  return useMutation({
    mutationFn: (input: { file: File; onProgress?: (f: number) => void }) => {
      const fd = new FormData();
      fd.append("file", input.file);
      return uploadFile<IntakeResponse>("/api/intakes", fd, input.onProgress);
    },
  });
}

export function useDeleteIntake() {
  return useMutation({
    mutationFn: (id: string) => apiFetch(`/api/intakes/${id}`, { method: "DELETE" }),
  });
}

export interface ConfirmIntakePayload {
  merchantName: string;
  orderNumber?: string | null;
  purchaseDate?: string | null;
  currency: string;
  totalAmount?: number | null;
  notes?: string | null;
  items: PurchaseItemInput[];
  provenance?: {
    merchantName?: { source: string; confidence?: number | null };
    orderNumber?: { source: string; confidence?: number | null };
    purchaseDate?: { source: string; confidence?: number | null };
    totalAmount?: { source: string; confidence?: number | null };
  };
}

export function useConfirmIntake() {
  const inv = useInvalidate();
  return useMutation({
    mutationFn: (input: { intakeId: string; payload: ConfirmIntakePayload }) =>
      apiFetch<PurchaseDetailResponse>(`/api/intakes/${input.intakeId}/confirm`, {
        method: "POST",
        body: JSON.stringify(input.payload),
        headers: json,
      }),
    onSuccess: () => inv.purchases(),
  });
}
