"use client";

import { ArrowLeftIcon, Loader2Icon, XIcon } from "lucide-react";
import { AnimatePresence, motion } from "motion/react";
import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";

import {
  useConfirmIntake,
  useCreatePurchase,
} from "@/lib/api/hooks";
import type { IntakeResponse } from "@/lib/api/types";
import { IntakeUploader } from "@/components/intake-uploader";
import { InlineError } from "@/components/inline-error";
import { Logo } from "@/components/app-shell";
import {
  prefillFromIntake,
  provenancePayload,
} from "@/components/new-case/state";
import {
  emptyPurchaseValues,
  PurchaseForm,
  validatePurchaseForm,
  type FieldMeta,
  type PurchaseFormValues,
} from "@/components/purchase-form";
import { Button } from "@/components/ui/button";

export default function NewPurchasePage() {
  const router = useRouter();
  const [step, setStep] = useState<"upload" | "details">("upload");
  const [intakeId, setIntakeId] = useState<string | null>(null);
  const [values, setValues] = useState<PurchaseFormValues>(emptyPurchaseValues);
  const [meta, setMeta] = useState<Record<string, FieldMeta>>({});
  const [errors, setErrors] = useState<Record<string, string>>({});
  const [submitError, setSubmitError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);

  const confirmIntake = useConfirmIntake();
  const createPurchase = useCreatePurchase();

  const onResolved = (intake: IntakeResponse) => {
    setIntakeId(intake.id);
    if (intake.status === "succeeded") {
      const { purchase, meta: m } = prefillFromIntake(intake);
      setValues(purchase);
      setMeta(m as Record<string, FieldMeta>);
    }
    setStep("details");
  };

  const save = async () => {
    const errs = validatePurchaseForm(values);
    setErrors(errs);
    if (Object.keys(errs).length > 0) return;
    setSaving(true);
    setSubmitError(null);
    const payload = {
      merchantName: values.merchantName.trim(),
      orderNumber: values.orderNumber.trim() || null,
      purchaseDate: values.purchaseDate || null,
      currency: values.currency,
      totalAmount: values.totalAmount.trim() === "" ? null : Number(values.totalAmount),
      notes: values.notes.trim() || null,
      items: values.items.map((item) => ({
        productName: item.productName.trim(),
        quantity: Number(item.quantity) || 1,
        unitPrice: item.unitPrice.trim() === "" ? null : Number(item.unitPrice),
      })),
    };
    try {
      const purchase = intakeId
        ? await confirmIntake.mutateAsync({
            intakeId,
            payload: { ...payload, provenance: provenancePayload(meta) },
          })
        : await createPurchase.mutateAsync(payload);
      toast.success("Purchase saved.");
      router.replace(`/app/purchases/${purchase.id}`);
    } catch (e) {
      setSubmitError(e);
      setSaving(false);
    }
  };

  return (
    <div className="mx-auto flex w-full max-w-xl flex-1 flex-col px-4 pb-10">
      <header className="flex items-center justify-between py-4">
        <Logo />
        <Button
          variant="ghost"
          size="icon"
          aria-label="Close"
          className="touch-target"
          onClick={() => router.push("/app/purchases")}
        >
          <XIcon aria-hidden />
        </Button>
      </header>

      <AnimatePresence mode="wait" initial={false}>
        <motion.div
          key={step}
          initial={{ opacity: 0, x: 24 }}
          animate={{ opacity: 1, x: 0 }}
          exit={{ opacity: 0, x: -24 }}
          transition={{ duration: 0.18 }}
          className="mt-4 flex-1"
        >
          {step === "upload" ? (
            <section className="space-y-3">
              <h1 className="text-xl font-semibold">Add your receipt or order confirmation</h1>
              <p className="text-sm text-muted-foreground">
                We&apos;ll read it and fill in the details for you.
              </p>
              <IntakeUploader onResolved={onResolved} />
              <Button variant="link" className="touch-target" onClick={() => setStep("details")}>
                I&apos;ll enter the details myself
              </Button>
            </section>
          ) : (
            <section className="space-y-4">
              <h1 className="text-xl font-semibold">
                {intakeId ? "Check the purchase details" : "Enter the purchase details"}
              </h1>
              <PurchaseForm values={values} onChange={setValues} errors={errors} meta={meta} />
              {submitError ? <InlineError error={submitError} onRetry={save} /> : null}
              <div className="flex items-center justify-between pt-2">
                <Button
                  variant="ghost"
                  className="touch-target"
                  onClick={() => setStep("upload")}
                  disabled={saving}
                >
                  <ArrowLeftIcon aria-hidden /> Back
                </Button>
                <Button className="touch-target" size="lg" onClick={save} disabled={saving}>
                  {saving ? (
                    <>
                      <Loader2Icon className="animate-spin" aria-hidden /> Saving…
                    </>
                  ) : (
                    "Save purchase"
                  )}
                </Button>
              </div>
            </section>
          )}
        </motion.div>
      </AnimatePresence>
    </div>
  );
}
