"use client";

import {
  ArrowLeftIcon,
  Loader2Icon,
  XIcon,
} from "lucide-react";
import { AnimatePresence, motion } from "motion/react";
import { useRouter, useSearchParams } from "next/navigation";
import {
  Suspense,
  useEffect,
  useMemo,
  useReducer,
  useRef,
  useState,
} from "react";
import { z } from "zod";

import {
  useConfirmIntake,
  useCreateCase,
  useCreatePurchase,
  usePurchase,
} from "@/lib/api/hooks";
import { problemTypeOptions, outcomeOptions, currencyOptions } from "@/lib/labels";
import { cn } from "@/lib/utils";
import {
  clearWizardState,
  canContinue,
  isRefund,
  nextStep,
  prefillFromIntake,
  prevStep,
  provenancePayload,
  restoreWizardState,
  stepPosition,
  WIZARD_STORAGE_KEY,
  wizardReducer,
  type WizardState,
} from "@/components/new-case/state";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { FormField } from "@/components/form-field";
import { InlineError } from "@/components/inline-error";
import { IntakeUploader } from "@/components/intake-uploader";
import { Logo } from "@/components/app-shell";
import { ProblemTypeIcon } from "@/components/problem-type-icon";
import {
  PurchaseForm,
  validatePurchaseForm,
  type FieldMeta,
} from "@/components/purchase-form";
import { Button } from "@/components/ui/button";
import { Checkbox } from "@/components/ui/checkbox";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { toast } from "sonner";

function OptionCard({
  selected,
  onClick,
  title,
  helper,
  icon,
}: {
  selected: boolean;
  onClick: () => void;
  title: string;
  helper: string;
  icon?: React.ReactNode;
}) {
  return (
    <button
      type="button"
      role="radio"
      aria-checked={selected}
      onClick={onClick}
      className={cn(
        "flex min-h-16 w-full items-center gap-3 rounded-xl border px-4 py-3 text-left transition-colors",
        selected
          ? "border-primary bg-primary/5 ring-1 ring-primary/30"
          : "border-border bg-card hover:border-primary/40",
      )}
    >
      {icon}
      <span className="min-w-0 flex-1">
        <span className="block text-sm font-medium">{title}</span>
        <span className="block text-xs text-muted-foreground">{helper}</span>
      </span>
      <span
        aria-hidden
        className={cn(
          "size-4 shrink-0 rounded-full border-2",
          selected ? "border-primary bg-primary" : "border-muted-foreground/40",
        )}
      />
    </button>
  );
}

const descriptionSchema = z.string().min(20, "A couple of sentences is enough — at least 20 characters.");

function Wizard() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const purchaseIdParam = searchParams.get("purchaseId");

  const [state, dispatch] = useReducer(wizardReducer, undefined, () => {
    const restored = restoreWizardState();
    if (purchaseIdParam) {
      return { ...restored, purchaseId: purchaseIdParam, purchase: null, intakeId: null };
    }
    return restored;
  });
  const set = (patch: Partial<WizardState>) => dispatch({ type: "set", patch });

  // Persist across reloads.
  useEffect(() => {
    window.sessionStorage.setItem(WIZARD_STORAGE_KEY, JSON.stringify(state));
  }, [state]);

  const [confirmDiscard, setConfirmDiscard] = useState(false);
  const [submitting, setSubmitting] = useState(false);
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [purchaseErrors, setPurchaseErrors] = useState<Record<string, string>>({});
  const [descTouched, setDescTouched] = useState(false);
  const headingRef = useRef<HTMLHeadingElement>(null);
  const { n, total } = stepPosition(state);

  const confirmIntake = useConfirmIntake();
  const createPurchase = useCreatePurchase();
  const createCase = useCreateCase();
  const { data: preselectedPurchase } = usePurchase(state.purchaseId ?? "");

  useEffect(() => {
    headingRef.current?.focus();
  }, [state.step]);

  const dirty =
    state.problemType !== null ||
    state.description !== "" ||
    state.intakeId !== null ||
    state.outcomeType !== null;

  const itemsForStep5 = useMemo(() => {
    if (state.purchaseId && preselectedPurchase) {
      return preselectedPurchase.items.map((i) => i.productName);
    }
    return (state.purchase?.items ?? []).map((i) => i.productName || "Unnamed item");
  }, [state.purchaseId, preselectedPurchase, state.purchase]);

  const updatePurchase = (values: NonNullable<WizardState["purchase"]>) => {
    const meta = { ...state.purchaseMeta };
    (Object.keys(meta) as (keyof typeof meta)[]).forEach((key) => {
      if (meta[key] && values[key] !== (state.purchase?.[key] ?? "")) {
        meta[key] = { ...meta[key]!, edited: true };
      }
    });
    set({ purchase: values, purchaseMeta: meta });
  };

  const continueStep = () => {
    if (state.step === 4 && state.purchase) {
      const errors = validatePurchaseForm(state.purchase);
      setPurchaseErrors(errors);
      if (Object.keys(errors).length > 0) return;
    }
    setSubmitError(null);
    set({ step: nextStep(state) });
  };

  const submit = async () => {
    const desc = descriptionSchema.safeParse(state.description.trim());
    if (!desc.success) {
      setDescTouched(true);
      return;
    }
    setSubmitting(true);
    setSubmitError(null);
    try {
      let purchaseId = state.purchaseId;
      let affectedItemIds: string[] | undefined;

      if (state.purchase && !purchaseId) {
        const payload = {
          merchantName: state.purchase.merchantName.trim(),
          orderNumber: state.purchase.orderNumber.trim() || null,
          purchaseDate: state.purchase.purchaseDate || null,
          currency: state.purchase.currency,
          totalAmount:
            state.purchase.totalAmount.trim() === ""
              ? null
              : Number(state.purchase.totalAmount),
          notes: null,
          items: state.purchase.items.map((item) => ({
            productName: item.productName.trim(),
            quantity: Number(item.quantity) || 1,
            unitPrice: item.unitPrice.trim() === "" ? null : Number(item.unitPrice),
          })),
        };
        const purchase = state.intakeId
          ? await confirmIntake.mutateAsync({
              intakeId: state.intakeId,
              payload: { ...payload, provenance: provenancePayload(state.purchaseMeta) },
            })
          : await createPurchase.mutateAsync(payload);
        purchaseId = purchase.id;
        affectedItemIds = purchase.items
          .filter((_, i) => state.affectedIndexes.includes(i))
          .map((i) => i.id);
      } else if (purchaseId && preselectedPurchase) {
        affectedItemIds = preselectedPurchase.items
          .filter((_, i) => state.affectedIndexes.includes(i))
          .map((i) => i.id);
      }

      const created = await createCase.mutateAsync({
        problemType: state.problemType!,
        description: state.description.trim(),
        requestedOutcomeType: state.outcomeType!,
        requestedAmount:
          isRefund(state.outcomeType) && state.outcomeAmount.trim() !== ""
            ? Number(state.outcomeAmount)
            : null,
        requestedCurrency: isRefund(state.outcomeType)
          ? state.outcomeCurrency
          : null,
        purchaseId,
        affectedItemIds: affectedItemIds && affectedItemIds.length > 0 ? affectedItemIds : null,
        problemDiscoveredOn: state.discoveredOn || null,
      });

      clearWizardState();
      toast.success("Your case is ready.");
      router.replace(`/app/cases/${created.id}?created=1`);
    } catch (error) {
      setSubmitError(error);
      setSubmitting(false);
    }
  };

  const descError = descTouched
    ? (() => {
        const r = descriptionSchema.safeParse(state.description.trim());
        return r.success ? undefined : r.error.issues[0]?.message;
      })()
    : undefined;

  return (
    <div className="mx-auto flex w-full max-w-xl flex-1 flex-col px-4 pb-10">
      <header className="flex items-center justify-between py-4">
        <Logo />
        <Button
          variant="ghost"
          size="icon"
          aria-label="Close"
          className="touch-target"
          onClick={() => (dirty ? setConfirmDiscard(true) : router.back())}
        >
          <XIcon aria-hidden />
        </Button>
      </header>

      <div aria-live="polite" className="sr-only">
        Step {n} of {total}
      </div>
      <p className="text-xs font-medium text-muted-foreground">
        Start a case · Step {n} of {total}
      </p>
      <div className="mt-1 h-1 w-full rounded-full bg-muted" aria-hidden>
        <div
          className="h-1 rounded-full bg-primary transition-all"
          style={{ width: `${(n / total) * 100}%` }}
        />
      </div>

      <AnimatePresence mode="wait" initial={false}>
        <motion.div
          key={state.step}
          initial={{ opacity: 0, x: 24 }}
          animate={{ opacity: 1, x: 0 }}
          exit={{ opacity: 0, x: -24 }}
          transition={{ duration: 0.18 }}
          className="mt-6 flex-1"
        >
          {state.step === 1 ? (
            <section aria-labelledby="step-what" className="space-y-3">
              <h1 id="step-what" ref={headingRef} tabIndex={-1} className="text-xl font-semibold focus:outline-none">What went wrong?</h1>
              <div role="radiogroup" aria-label="Problem type" className="space-y-2 pt-2">
                {problemTypeOptions.map((opt) => (
                  <OptionCard
                    key={opt.value}
                    selected={state.problemType === opt.value}
                    onClick={() => set({ problemType: opt.value })}
                    title={opt.label}
                    helper={opt.helper}
                    icon={<ProblemTypeIcon type={opt.value} className="size-5 text-primary" />}
                  />
                ))}
              </div>
            </section>
          ) : null}

          {state.step === 2 ? (
            <section aria-labelledby="step-outcome" className="space-y-3">
              <h1 id="step-outcome" ref={headingRef} tabIndex={-1} className="text-xl font-semibold focus:outline-none">
                What would you like the seller to do?
              </h1>
              <div role="radiogroup" aria-label="Requested outcome" className="space-y-2 pt-2">
                {outcomeOptions.map((opt) => (
                  <OptionCard
                    key={opt.value}
                    selected={state.outcomeType === opt.value}
                    onClick={() => set({ outcomeType: opt.value })}
                    title={opt.label}
                    helper={opt.helper}
                  />
                ))}
              </div>
              {isRefund(state.outcomeType) ? (
                <div className="grid gap-3 pt-2 sm:grid-cols-[7rem_1fr]">
                  <FormField label="Currency" htmlFor="outcome-currency">
                    <Select
                      value={state.outcomeCurrency}
                      onValueChange={(v) => set({ outcomeCurrency: String(v) })}
                    >
                      <SelectTrigger id="outcome-currency" className="w-full">
                        <SelectValue />
                      </SelectTrigger>
                      <SelectContent>
                        {currencyOptions.map((c) => (
                          <SelectItem key={c} value={c}>{c}</SelectItem>
                        ))}
                      </SelectContent>
                    </Select>
                  </FormField>
                  <FormField label="Amount" htmlFor="outcome-amount" required>
                    <Input
                      id="outcome-amount"
                      inputMode="decimal"
                      value={state.outcomeAmount}
                      onChange={(e) => set({ outcomeAmount: e.target.value })}
                      placeholder="0.00"
                    />
                  </FormField>
                </div>
              ) : null}
            </section>
          ) : null}

          {state.step === 3 ? (
            <section aria-labelledby="step-receipt" className="space-y-3">
              <h1 id="step-receipt" ref={headingRef} tabIndex={-1} className="text-xl font-semibold focus:outline-none">
                Do you have your receipt or order confirmation?
              </h1>
              <p className="text-sm text-muted-foreground">
                We&apos;ll read it and pre-fill the purchase details for you.
              </p>
              <IntakeUploader
                onFileChanged={() => {
                  set({ intakeStatus: "processing" });
                }}
                onResolved={(intake) => {
                  if (intake.status === "succeeded") {
                    const { purchase, meta } = prefillFromIntake(intake);
                    set({
                      intakeId: intake.id,
                      intakeStatus: "succeeded",
                      purchase,
                      purchaseMeta: meta as Record<string, FieldMeta>,
                    });
                  } else {
                    set({
                      intakeId: intake.id,
                      intakeStatus: "failed",
                      purchase: { ...(state.purchase ?? { merchantName: "", orderNumber: "", purchaseDate: "", currency: "EUR", totalAmount: "", notes: "", items: [{ productName: "", quantity: "1", unitPrice: "" }] }) },
                    });
                  }
                  set({ step: nextStep(state) });
                }}
              />
              <Button
                variant="link"
                className="touch-target"
                onClick={() => set({ step: nextStep(state), intakeStatus: "none" })}
              >
                Skip for now — I&apos;ll add it later
              </Button>
            </section>
          ) : null}

          {state.step === 4 ? (
            <section aria-labelledby="step-details" className="space-y-3">
              <h1 id="step-details" ref={headingRef} tabIndex={-1} className="text-xl font-semibold focus:outline-none">
                {state.intakeStatus === "succeeded"
                  ? "Check the purchase details"
                  : "Enter the purchase details"}
              </h1>
              {state.purchase ? (
                <PurchaseForm
                  values={state.purchase}
                  onChange={updatePurchase}
                  errors={purchaseErrors}
                  meta={state.purchaseMeta}
                  idPrefix="wiz"
                />
              ) : null}
              {state.intakeStatus === "none" ? (
                <Button
                  variant="link"
                  className="touch-target"
                  onClick={() => set({ purchase: null, step: nextStep(state) })}
                >
                  I&apos;ll add purchase details later
                </Button>
              ) : null}
            </section>
          ) : null}

          {state.step === 5 ? (
            <section aria-labelledby="step-finish" className="space-y-5">
              <h1 id="step-finish" ref={headingRef} tabIndex={-1} className="text-xl font-semibold focus:outline-none">
                {state.purchase || state.purchaseId
                  ? "Which item is affected, and what happened?"
                  : "What happened?"}
              </h1>
              {itemsForStep5.length > 0 && (state.purchase || state.purchaseId) ? (
                <fieldset className="space-y-2">
                  <legend className="text-sm font-medium">Affected items</legend>
                  {itemsForStep5.map((name, i) => (
                    <label
                      key={i}
                      className="flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border border-border bg-card px-4 py-3"
                    >
                      <Checkbox
                        checked={state.affectedIndexes.includes(i)}
                        onCheckedChange={(checked) =>
                          set({
                            affectedIndexes: checked
                              ? [...state.affectedIndexes, i]
                              : state.affectedIndexes.filter((j) => j !== i),
                          })
                        }
                      />
                      <span className="text-sm">{name}</span>
                    </label>
                  ))}
                </fieldset>
              ) : null}
              <FormField
                label="What happened?"
                htmlFor="description"
                required
                error={descError}
                hint="A couple of sentences is enough. What arrived, what's wrong, when you noticed."
              >
                <Textarea
                  id="description"
                  rows={4}
                  value={state.description}
                  onChange={(e) => {
                    setDescTouched(true);
                    set({ description: e.target.value });
                  }}
                />
              </FormField>
              <FormField label="When did you notice the problem?" htmlFor="discovered">
                <Input
                  id="discovered"
                  type="date"
                  value={state.discoveredOn}
                  onChange={(e) => set({ discoveredOn: e.target.value })}
                />
              </FormField>
            </section>
          ) : null}
        </motion.div>
      </AnimatePresence>

      {submitError ? <InlineError error={submitError} onRetry={submit} /> : null}

      <footer className="mt-8 flex items-center justify-between gap-3">
        <Button
          variant="ghost"
          className="touch-target"
          onClick={() => set({ step: prevStep(state) })}
          disabled={n === 1 || submitting}
        >
          <ArrowLeftIcon aria-hidden /> Back
        </Button>
        {state.step === 5 ? (
          <Button
            className="touch-target"
            size="lg"
            onClick={submit}
            disabled={submitting || !canContinue(state)}
          >
            {submitting ? (
              <>
                <Loader2Icon className="animate-spin" aria-hidden /> Creating…
              </>
            ) : (
              "Create case"
            )}
          </Button>
        ) : (
          <Button
            className="touch-target"
            size="lg"
            onClick={continueStep}
            disabled={!canContinue(state)}
          >
            Next
          </Button>
        )}
      </footer>

      <ConfirmDialog
        open={confirmDiscard}
        onOpenChange={setConfirmDiscard}
        title="Discard this case?"
        description="The details you've entered so far will be lost."
        confirmLabel="Discard"
        onConfirm={() => {
          dispatch({ type: "reset" });
          clearWizardState();
          router.push("/app");
        }}
      />
    </div>
  );
}

export default function NewCasePage() {
  return (
    <Suspense>
      <Wizard />
    </Suspense>
  );
}
