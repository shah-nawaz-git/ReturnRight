import type {
  IntakeResponse,
  ProblemType,
  RequestedOutcomeType,
} from "@/lib/api/types";
import type { FieldMeta, PurchaseFormValues } from "@/components/purchase-form";
import { emptyPurchaseValues } from "@/components/purchase-form";

export const WIZARD_STORAGE_KEY = "rr.newCase";
export const TOTAL_STEPS = 5;

export type WizardStep = 1 | 2 | 3 | 4 | 5;

export interface WizardState {
  step: WizardStep;
  problemType: ProblemType | null;
  outcomeType: RequestedOutcomeType | null;
  outcomeAmount: string;
  outcomeCurrency: string;
  /** Pre-selected existing purchase (?purchaseId=) — skips steps 3–4. */
  purchaseId: string | null;
  intakeId: string | null;
  intakeStatus: "none" | "processing" | "succeeded" | "failed";
  /** null = user chose "add purchase details later". */
  purchase: PurchaseFormValues | null;
  purchaseMeta: Partial<
    Record<"merchantName" | "orderNumber" | "purchaseDate" | "totalAmount", FieldMeta>
  >;
  /** Indexes into purchase.items that are affected by the problem. */
  affectedIndexes: number[];
  description: string;
  discoveredOn: string;
}

export const initialWizardState: WizardState = {
  step: 1,
  problemType: null,
  outcomeType: null,
  outcomeAmount: "",
  outcomeCurrency: "EUR",
  purchaseId: null,
  intakeId: null,
  intakeStatus: "none",
  purchase: { ...emptyPurchaseValues, items: [{ productName: "", quantity: "1", unitPrice: "" }] },
  purchaseMeta: {},
  affectedIndexes: [0],
  description: "",
  discoveredOn: "",
};

export type WizardAction =
  | { type: "set"; patch: Partial<WizardState> }
  | { type: "reset" };

export function wizardReducer(state: WizardState, action: WizardAction): WizardState {
  switch (action.type) {
    case "set":
      return { ...state, ...action.patch };
    case "reset":
      return { ...initialWizardState };
  }
}

export function isRefund(type: RequestedOutcomeType | null): boolean {
  return type === "FullRefund" || type === "PartialRefund";
}

/** The step sequence — 3 and 4 vanish when a purchase is preselected. */
export function stepFlow(state: WizardState): WizardStep[] {
  return state.purchaseId ? [1, 2, 5] : [1, 2, 3, 4, 5];
}

export function nextStep(state: WizardState): WizardStep {
  const flow = stepFlow(state);
  return flow[Math.min(flow.indexOf(state.step) + 1, flow.length - 1)];
}

export function prevStep(state: WizardState): WizardStep {
  const flow = stepFlow(state);
  return flow[Math.max(flow.indexOf(state.step) - 1, 0)];
}

/** Step position for the "Step n of 5" label (focused flow counts 3). */
export function stepPosition(state: WizardState): { n: number; total: number } {
  const flow = stepFlow(state);
  return { n: flow.indexOf(state.step) + 1, total: flow.length };
}

export function canContinue(state: WizardState): boolean {
  switch (state.step) {
    case 1:
      return state.problemType !== null;
    case 2:
      if (state.outcomeType === null) return false;
      if (isRefund(state.outcomeType)) {
        const amount = Number(state.outcomeAmount);
        return state.outcomeAmount.trim() !== "" && Number.isFinite(amount) && amount > 0;
      }
      return true;
    case 3:
      return true; // skip / resolved intake both continue
    case 4:
      return true; // PurchaseForm validates on submit
    case 5:
      return state.description.trim().length >= 20;
  }
}

export function restoreWizardState(): WizardState {
  if (typeof window === "undefined") return initialWizardState;
  try {
    const raw = window.sessionStorage.getItem(WIZARD_STORAGE_KEY);
    if (!raw) return initialWizardState;
    const parsed = JSON.parse(raw) as WizardState;
    return { ...initialWizardState, ...parsed };
  } catch {
    return initialWizardState;
  }
}

export function clearWizardState(): void {
  if (typeof window === "undefined") return;
  window.sessionStorage.removeItem(WIZARD_STORAGE_KEY);
}

/** Map intake candidates into purchase form values + provenance. */
export function prefillFromIntake(intake: IntakeResponse): {
  purchase: PurchaseFormValues;
  meta: WizardState["purchaseMeta"];
} {
  const candidate = (field: string) =>
    intake.candidates.find((c) => c.field === field);

  const purchase = { ...emptyPurchaseValues };
  const meta: WizardState["purchaseMeta"] = {};

  const merchant = candidate("merchant");
  if (merchant) {
    purchase.merchantName = merchant.value;
    meta.merchantName = { confidence: merchant.confidence, edited: false };
  }
  const order = candidate("order_number");
  if (order) {
    purchase.orderNumber = order.value;
    meta.orderNumber = { confidence: order.confidence, edited: false };
  }
  const date = candidate("purchase_date");
  if (date) {
    purchase.purchaseDate = date.value;
    meta.purchaseDate = { confidence: date.confidence, edited: false };
  }
  const currency = candidate("currency");
  if (currency && /^[A-Z]{3}$/.test(currency.value)) {
    purchase.currency = currency.value;
  }
  const total = candidate("total");
  if (total) {
    purchase.totalAmount = total.value;
    meta.totalAmount = { confidence: total.confidence, edited: false };
  }
  if (intake.items.length > 0) {
    purchase.items = intake.items.map((item) => ({
      productName: item.name,
      quantity: String(item.quantity ?? 1),
      unitPrice: item.unitPrice !== null ? String(item.unitPrice) : "",
    }));
  }
  return { purchase, meta };
}

export function provenancePayload(
  meta: WizardState["purchaseMeta"],
): Record<
  "merchantName" | "orderNumber" | "purchaseDate" | "totalAmount",
  { source: string; confidence?: number | null }
> {
  const entry = (key: keyof NonNullable<WizardState["purchaseMeta"]>) => {
    const m = meta[key];
    if (m && !m.edited && m.confidence !== null) {
      return { source: "ExtractedFromDocument", confidence: m.confidence };
    }
    return { source: "UserEntered" };
  };
  return {
    merchantName: entry("merchantName"),
    orderNumber: entry("orderNumber"),
    purchaseDate: entry("purchaseDate"),
    totalAmount: entry("totalAmount"),
  };
}
