"use client";

import { Loader2Icon } from "lucide-react";
import { useState } from "react";

import { problemTitle } from "@/lib/api/client";
import {
  useChangeCaseStatus,
  useCreateFollowUp,
  useCreateInteraction,
  useDeleteInteractionAttachment,
  useResolveCase,
  useUpdateCase,
  useUpdateFollowUp,
  useUpdateInteraction,
  useUploadEvidence,
  useUploadInteractionAttachment,
} from "@/lib/api/hooks";
import type {
  CaseDetailResponse,
  EvidenceType,
  InteractionResponse,
  InteractionType,
  FollowUpResponse,
  ProblemType,
  RequestedOutcomeType,
} from "@/lib/api/types";
import { isRefund } from "@/components/new-case/state";
import {
  activeStatusOptions,
  currencyOptions,
  evidenceTypeOptions,
  finalOutcomeOptions,
  interactionTypeGroups,
  outcomeOptions,
  problemTypeOptions,
  statusLabels,
} from "@/lib/labels";
import { toLocalInputValue } from "@/lib/format";
import { FileDropZone } from "@/components/file-drop-zone";
import { FormField } from "@/components/form-field";
import { Button } from "@/components/ui/button";
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Input } from "@/components/ui/input";
import {
  Select,
  SelectContent,
  SelectGroup,
  SelectItem,
  SelectLabel,
  SelectTrigger,
  SelectValue,
} from "@/components/ui/select";
import { Textarea } from "@/components/ui/textarea";
import { RadioGroup, RadioGroupItem } from "@/components/ui/radio-group";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";

const dlgClass = "sm:max-w-lg max-h-[85dvh] overflow-y-auto";

function DialogError({ error }: { error: unknown }) {
  if (!error) return null;
  return (
    <p role="alert" className="rounded-lg bg-destructive/10 px-3 py-2 text-sm text-destructive">
      {problemTitle(error)}
    </p>
  );
}

function interactionLabel(t: InteractionType) {
  return interactionTypeGroups
    .flatMap((g) => g.options)
    .find((o) => o.value === t)?.label ?? t;
}

function InteractionTypeSelect({
  id,
  value,
  onChange,
}: {
  id: string;
  value: InteractionType;
  onChange: (v: InteractionType) => void;
}) {
  return (
    <Select value={value} onValueChange={(v) => onChange(v as InteractionType)}>
      <SelectTrigger id={id} className="w-full">
        <SelectValue>{interactionLabel(value)}</SelectValue>
      </SelectTrigger>
      <SelectContent>
        {interactionTypeGroups.map((group) => (
          <SelectGroup key={group.group}>
            <SelectLabel>{group.group}</SelectLabel>
            {group.options.map((opt) => (
              <SelectItem key={opt.value} value={opt.value}>
                {opt.label}
              </SelectItem>
            ))}
          </SelectGroup>
        ))}
      </SelectContent>
    </Select>
  );
}

const isPromiseType = (t: InteractionType) =>
  t === "RefundPromised" || t === "ReplacementPromised";

// ---------------------------------------------------------------------------
// Record / edit update

export function RecordUpdateDialog({
  caseId,
  interaction,
  open,
  onOpenChange,
}: {
  caseId: string;
  interaction: InteractionResponse | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const editing = interaction !== null;
  const create = useCreateInteraction(caseId);
  const update = useUpdateInteraction(caseId);
  const uploadAttachment = useUploadInteractionAttachment(caseId);
  const deleteAttachment = useDeleteInteractionAttachment(caseId);

  const [type, setType] = useState<InteractionType>(
    interaction?.interactionType ?? "ContactedSeller",
  );
  const [occurredAt, setOccurredAt] = useState(() =>
    interaction ? toLocalInputValue(interaction.occurredAt) : toLocalInputValue(new Date()),
  );
  const [note, setNote] = useState(interaction?.note ?? "");
  const [expectedBy, setExpectedBy] = useState("");
  const [promisedAmount, setPromisedAmount] = useState("");
  const [error, setError] = useState<unknown>(null);
  const [saving, setSaving] = useState(false);
  const [created, setCreated] = useState<InteractionResponse | null>(interaction);
  const [uploadProgress, setUploadProgress] = useState<number | null>(null);

  const submit = async () => {
    if (note.trim().length === 0) {
      setError({ problem: { title: "Write a short note about what happened." } });
      return;
    }
    setSaving(true);
    setError(null);
    try {
      const payload = {
        interactionType: type,
        occurredAt: new Date(occurredAt).toISOString(),
        note: note.trim(),
        expectedBy: isPromiseType(type) && expectedBy ? expectedBy : null,
        promisedAmount:
          isPromiseType(type) && promisedAmount.trim() !== ""
            ? Number(promisedAmount)
            : null,
      };
      if (editing) {
        await update.mutateAsync({
          interactionId: interaction.id,
          interactionType: payload.interactionType,
          occurredAt: payload.occurredAt,
          note: payload.note,
        });
      } else {
        setCreated(await create.mutateAsync(payload));
      }
      toast.success(editing ? "Update saved." : "Update recorded.");
      if (editing) onOpenChange(false);
    } catch (e) {
      setError(e);
    } finally {
      setSaving(false);
    }
  };

  const attach = async (file: File) => {
    if (!created) return;
    try {
      await uploadAttachment.mutateAsync({
        interactionId: created.id,
        file,
        onProgress: setUploadProgress,
      });
      setUploadProgress(null);
      toast.success("Attachment added.");
      onOpenChange(false);
    } catch (e) {
      setUploadProgress(null);
      setError(e);
    }
  };

  const target = editing ? interaction : created;

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={dlgClass}>
        <DialogHeader>
          <DialogTitle>{editing ? "Edit update" : "Record an update"}</DialogTitle>
          <DialogDescription>
            What happened between you and the seller?
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <FormField label="Type of update" htmlFor="iu-type">
            <InteractionTypeSelect id="iu-type" value={type} onChange={setType} />
          </FormField>
          <FormField label="When" htmlFor="iu-when">
            <Input
              id="iu-when"
              type="datetime-local"
              value={occurredAt}
              onChange={(e) => setOccurredAt(e.target.value)}
            />
          </FormField>
          {isPromiseType(type) ? (
            <div className="grid gap-3 sm:grid-cols-2">
              <FormField label="Expected by" htmlFor="iu-expected">
                <Input
                  id="iu-expected"
                  type="date"
                  value={expectedBy}
                  onChange={(e) => setExpectedBy(e.target.value)}
                />
              </FormField>
              <FormField label="Promised amount" htmlFor="iu-amount">
                <Input
                  id="iu-amount"
                  inputMode="decimal"
                  value={promisedAmount}
                  onChange={(e) => setPromisedAmount(e.target.value)}
                  placeholder="0.00"
                />
              </FormField>
            </div>
          ) : null}
          <FormField label="Note" htmlFor="iu-note" required>
            <Textarea
              id="iu-note"
              rows={3}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              placeholder="e.g. Emailed them photos and asked for a refund."
            />
          </FormField>
          {created || editing ? (
            target && !target.document ? (
              <FormField label="Attachment (optional)" htmlFor="iu-file">
                <FileDropZone
                  onFile={attach}
                  progress={uploadProgress}
                  label="Add a screenshot or file"
                />
              </FormField>
            ) : (
              <p className="text-xs text-muted-foreground">
                Attachment: {target?.document?.fileName}{" "}
                <button
                  type="button"
                  className="text-destructive underline-offset-2 hover:underline"
                  onClick={async () => {
                    if (target) {
                      await deleteAttachment.mutateAsync(target.id);
                      toast.success("Attachment removed.");
                    }
                  }}
                >
                  remove
                </button>
              </p>
            )
          ) : null}
          <DialogError error={error} />
        </div>
        <DialogFooter>
          <Button
            className="touch-target"
            onClick={submit}
            disabled={saving}
          >
            {saving ? (
              <>
                <Loader2Icon className="animate-spin" aria-hidden /> Saving…
              </>
            ) : created && !editing ? (
              "Save again"
            ) : editing ? (
              "Save changes"
            ) : (
              "Record update"
            )}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ---------------------------------------------------------------------------
// Add evidence

export function AddEvidenceDialog({
  caseId,
  initialType = "DamagePhoto",
  open,
  onOpenChange,
}: {
  caseId: string;
  initialType?: EvidenceType;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const upload = useUploadEvidence(caseId);
  const [type, setType] = useState<EvidenceType>(initialType);
  const [description, setDescription] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [progress, setProgress] = useState<number | null>(null);
  const [error, setError] = useState<unknown>(null);

  const submit = async () => {
    if (!file) {
      setError({ problem: { title: "Choose a file first." } });
      return;
    }
    setError(null);
    try {
      await upload.mutateAsync({
        file,
        evidenceType: type,
        description: description.trim() || undefined,
        onProgress: setProgress,
      });
      toast.success("Evidence added.");
      onOpenChange(false);
    } catch (e) {
      setProgress(null);
      setError(e);
    }
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={dlgClass}>
        <DialogHeader>
          <DialogTitle>Add evidence</DialogTitle>
          <DialogDescription>Photos, screenshots or documents that support your case.</DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <FormField label="What kind of evidence?" htmlFor="ev-type">
            <Select value={type} onValueChange={(v) => setType(v as EvidenceType)}>
              <SelectTrigger id="ev-type" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {evidenceTypeOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>
                    {opt.label} — {opt.helper}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FormField>
          <FileDropZone
            onFile={setFile}
            progress={progress}
            file={file ? { name: file.name, size: file.size } : null}
            onClear={() => setFile(null)}
          />
          <FormField label="Description (optional)" htmlFor="ev-desc">
            <Input
              id="ev-desc"
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="e.g. Photo of the cracked headband"
              maxLength={500}
            />
          </FormField>
          <DialogError error={error} />
        </div>
        <DialogFooter>
          <Button className="touch-target" onClick={submit} disabled={progress !== null}>
            {progress !== null ? "Uploading…" : "Add evidence"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ---------------------------------------------------------------------------
// Schedule / move follow-up

const quickSuggestions = ["Check for seller reply", "Chase refund", "Confirm return received"];

function defaultDue(): string {
  const d = new Date();
  d.setDate(d.getDate() + 3);
  d.setHours(9, 0, 0, 0);
  return toLocalInputValue(d);
}

export function ScheduleFollowUpDialog({
  caseId,
  followUp,
  open,
  onOpenChange,
}: {
  caseId: string;
  followUp: FollowUpResponse | null;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const editing = followUp !== null;
  const create = useCreateFollowUp(caseId);
  const update = useUpdateFollowUp(caseId);
  const [title, setTitle] = useState(followUp?.title ?? "");
  const [dueAt, setDueAt] = useState(() =>
    followUp ? toLocalInputValue(followUp.dueAt) : defaultDue(),
  );
  const [error, setError] = useState<unknown>(null);

  const submit = async () => {
    if (!title.trim()) {
      setError({ problem: { title: "Give the follow-up a title." } });
      return;
    }
    setError(null);
    try {
      if (editing) {
        await update.mutateAsync({
          followUpId: followUp.id,
          title: title.trim(),
          dueAt: new Date(dueAt).toISOString(),
        });
        toast.success("Follow-up moved.");
      } else {
        await create.mutateAsync({
          title: title.trim(),
          dueAt: new Date(dueAt).toISOString(),
        });
        toast.success("Follow-up scheduled.");
      }
      onOpenChange(false);
    } catch (e) {
      setError(e);
    }
  };

  const pick = (days: number) => {
    const d = new Date();
    d.setDate(d.getDate() + days);
    d.setHours(9, 0, 0, 0);
    setDueAt(toLocalInputValue(d));
  };

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={dlgClass}>
        <DialogHeader>
          <DialogTitle>{editing ? "Move follow-up" : "Schedule a follow-up"}</DialogTitle>
          <DialogDescription>
            We&apos;ll remind you by email and in the app when it&apos;s due.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <FormField label="Title" htmlFor="fu-title" required>
            <Input
              id="fu-title"
              value={title}
              onChange={(e) => setTitle(e.target.value)}
              maxLength={200}
              placeholder="Check for seller reply"
            />
          </FormField>
          <div className="flex flex-wrap gap-1.5">
            {quickSuggestions.map((s) => (
              <button
                key={s}
                type="button"
                onClick={() => setTitle(s)}
                className="touch-target rounded-full border border-border px-3 py-1 text-xs text-muted-foreground hover:border-primary/50 hover:text-foreground"
              >
                {s}
              </button>
            ))}
          </div>
          <FormField label="Remind me" htmlFor="fu-due" required>
            <Input
              id="fu-due"
              type="datetime-local"
              value={dueAt}
              onChange={(e) => setDueAt(e.target.value)}
            />
          </FormField>
          <div className="flex flex-wrap gap-1.5">
            {[
              ["In 3 days", 3],
              ["In 1 week", 7],
              ["In 2 weeks", 14],
            ].map(([label, days]) => (
              <button
                key={label as string}
                type="button"
                onClick={() => pick(days as number)}
                className="touch-target rounded-full border border-border px-3 py-1 text-xs text-muted-foreground hover:border-primary/50 hover:text-foreground"
              >
                {label}
              </button>
            ))}
          </div>
          <DialogError error={error} />
        </div>
        <DialogFooter>
          <Button
            className="touch-target"
            onClick={submit}
            disabled={create.isPending || update.isPending}
          >
            {editing ? "Move it" : "Schedule"}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ---------------------------------------------------------------------------
// Change status

export function ChangeStatusDialog({
  issueCase,
  open,
  onOpenChange,
}: {
  issueCase: CaseDetailResponse;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const change = useChangeCaseStatus(issueCase.id);
  const [status, setStatus] = useState(issueCase.status);
  const [note, setNote] = useState("");
  const [error, setError] = useState<unknown>(null);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={dlgClass}>
        <DialogHeader>
          <DialogTitle>Change status</DialogTitle>
        </DialogHeader>
        <RadioGroup
          value={status}
          onValueChange={(v) => setStatus(v as typeof status)}
          className="space-y-2"
        >
          {activeStatusOptions.map((s) => (
            <Label
              key={s}
              className="flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border border-border bg-card px-4 py-3 text-sm font-normal"
            >
              <RadioGroupItem value={s} />
              {statusLabels[s]}
            </Label>
          ))}
        </RadioGroup>
        <FormField label="Note (optional)" htmlFor="cs-note">
          <Input
            id="cs-note"
            value={note}
            onChange={(e) => setNote(e.target.value)}
            maxLength={500}
          />
        </FormField>
        <DialogError error={error} />
        <DialogFooter>
          <Button
            className="touch-target"
            disabled={change.isPending || status === issueCase.status}
            onClick={async () => {
              try {
                await change.mutateAsync({ status, note: note.trim() || null });
                toast.success("Status changed.");
                onOpenChange(false);
              } catch (e) {
                setError(e);
              }
            }}
          >
            Change status
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ---------------------------------------------------------------------------
// Resolve

export function ResolveDialog({
  issueCase,
  open,
  onOpenChange,
}: {
  issueCase: CaseDetailResponse;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const resolve = useResolveCase(issueCase.id);
  const [outcome, setOutcome] = useState("FullRefundReceived");
  const [amount, setAmount] = useState(issueCase.requestedAmount?.toString() ?? "");
  const [currency, setCurrency] = useState(issueCase.requestedCurrency ?? "EUR");
  const [on, setOn] = useState(() => new Date().toISOString().slice(0, 10));
  const [note, setNote] = useState("");
  const [error, setError] = useState<unknown>(null);

  const isRefund = outcome === "FullRefundReceived" || outcome === "PartialRefundReceived";

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={dlgClass}>
        <DialogHeader>
          <DialogTitle>Resolve this case</DialogTitle>
          <DialogDescription>
            This will stop scheduled reminders. You can reopen the case later if
            anything changes.
          </DialogDescription>
        </DialogHeader>
        <div className="space-y-4">
          <RadioGroup value={outcome} onValueChange={setOutcome} className="space-y-2">
            {finalOutcomeOptions.map((opt) => (
              <Label
                key={opt.value}
                className="flex min-h-11 cursor-pointer items-center gap-3 rounded-xl border border-border bg-card px-4 py-3 text-sm font-normal"
              >
                <RadioGroupItem value={opt.value} />
                <span>
                  <span className="block">{opt.label}</span>
                  <span className="block text-xs text-muted-foreground">{opt.helper}</span>
                </span>
              </Label>
            ))}
          </RadioGroup>
          {isRefund ? (
            <div className="grid gap-3 sm:grid-cols-[7rem_1fr]">
              <FormField label="Currency" htmlFor="res-currency">
                <Select value={currency} onValueChange={(v) => setCurrency(String(v))}>
                  <SelectTrigger id="res-currency" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((c) => (
                      <SelectItem key={c} value={c}>{c}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </FormField>
              <FormField label="Amount received" htmlFor="res-amount">
                <Input
                  id="res-amount"
                  inputMode="decimal"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  placeholder="0.00"
                />
              </FormField>
            </div>
          ) : null}
          <FormField label="Date" htmlFor="res-date">
            <Input id="res-date" type="date" value={on} onChange={(e) => setOn(e.target.value)} />
          </FormField>
          <FormField label="Note (optional)" htmlFor="res-note">
            <Textarea
              id="res-note"
              rows={2}
              value={note}
              onChange={(e) => setNote(e.target.value)}
              maxLength={2000}
            />
          </FormField>
          <DialogError error={error} />
        </div>
        <DialogFooter>
          <Button
            className="touch-target"
            disabled={resolve.isPending}
            onClick={async () => {
              try {
                await resolve.mutateAsync({
                  finalOutcomeType: outcome as never,
                  finalAmount: isRefund && amount.trim() !== "" ? Number(amount) : null,
                  finalCurrency: isRefund ? currency : null,
                  completedOn: on || null,
                  note: note.trim() || null,
                });
                toast.success("Case resolved.");
                onOpenChange(false);
              } catch (e) {
                setError(e);
              }
            }}
          >
            Resolve case
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

// ---------------------------------------------------------------------------
// Edit case

export function EditCaseDialog({
  issueCase,
  open,
  onOpenChange,
}: {
  issueCase: CaseDetailResponse;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}) {
  const update = useUpdateCase(issueCase.id);
  const [problemType, setProblemType] = useState(issueCase.problemType);
  const [outcomeType, setOutcomeType] = useState(issueCase.requestedOutcomeType);
  const [amount, setAmount] = useState(issueCase.requestedAmount?.toString() ?? "");
  const [currency, setCurrency] = useState(issueCase.requestedCurrency ?? "EUR");
  const [description, setDescription] = useState(issueCase.description);
  const [discoveredOn, setDiscoveredOn] = useState(issueCase.problemDiscoveredOn ?? "");
  const [error, setError] = useState<unknown>(null);

  const refund = isRefund(outcomeType as RequestedOutcomeType);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className={dlgClass}>
        <DialogHeader>
          <DialogTitle>Edit case</DialogTitle>
        </DialogHeader>
        <div className="space-y-4">
          <FormField label="Problem type" htmlFor="ec-problem">
            <Select value={problemType} onValueChange={(v) => setProblemType(v as ProblemType)}>
              <SelectTrigger id="ec-problem" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {problemTypeOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FormField>
          <FormField label="Requested outcome" htmlFor="ec-outcome">
            <Select value={outcomeType} onValueChange={(v) => setOutcomeType(v as RequestedOutcomeType)}>
              <SelectTrigger id="ec-outcome" className="w-full">
                <SelectValue />
              </SelectTrigger>
              <SelectContent>
                {outcomeOptions.map((opt) => (
                  <SelectItem key={opt.value} value={opt.value}>{opt.label}</SelectItem>
                ))}
              </SelectContent>
            </Select>
          </FormField>
          {refund ? (
            <div className="grid gap-3 sm:grid-cols-[7rem_1fr]">
              <FormField label="Currency" htmlFor="ec-currency">
                <Select value={currency} onValueChange={(v) => setCurrency(String(v))}>
                  <SelectTrigger id="ec-currency" className="w-full">
                    <SelectValue />
                  </SelectTrigger>
                  <SelectContent>
                    {currencyOptions.map((c) => (
                      <SelectItem key={c} value={c}>{c}</SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              </FormField>
              <FormField label="Amount" htmlFor="ec-amount">
                <Input
                  id="ec-amount"
                  inputMode="decimal"
                  value={amount}
                  onChange={(e) => setAmount(e.target.value)}
                  placeholder="0.00"
                />
              </FormField>
            </div>
          ) : null}
          <FormField label="What happened" htmlFor="ec-desc" required>
            <Textarea
              id="ec-desc"
              rows={4}
              value={description}
              onChange={(e) => setDescription(e.target.value)}
            />
          </FormField>
          <FormField label="Noticed on" htmlFor="ec-date">
            <Input
              id="ec-date"
              type="date"
              value={discoveredOn}
              onChange={(e) => setDiscoveredOn(e.target.value)}
            />
          </FormField>
          <DialogError error={error} />
        </div>
        <DialogFooter>
          <Button
            className="touch-target"
            disabled={update.isPending || description.trim().length === 0}
            onClick={async () => {
              try {
                await update.mutateAsync({
                  problemType,
                  description: description.trim(),
                  requestedOutcomeType: outcomeType,
                  requestedAmount: refund && amount.trim() !== "" ? Number(amount) : null,
                  requestedCurrency: refund ? currency : null,
                  problemDiscoveredOn: discoveredOn || null,
                });
                toast.success("Case updated.");
                onOpenChange(false);
              } catch (e) {
                setError(e);
              }
            }}
          >
            Save changes
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

