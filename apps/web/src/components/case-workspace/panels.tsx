"use client";

import {
  AlertTriangleIcon,
  ArrowRightIcon,
  BellIcon,
  CalendarClockIcon,
  CheckCircle2Icon,
  CheckIcon,
  CircleIcon,
  FileTextIcon,
  ImageIcon,
  MessageSquareIcon,
  MoreHorizontalIcon,
  PaperclipIcon,
  PackageIcon,
  PartyPopperIcon,
  PencilIcon,
  PlusIcon,
  Trash2Icon,
  Undo2Icon,
} from "lucide-react";
import Link from "next/link";
import { useState } from "react";

import {
  useCancelFollowUp,
  useCompleteFollowUp,
  useDeleteEvidence,
  useDeleteInteraction,
  useDismissNextAction,
  useCaseReminders,
  useUpdateEvidence,
} from "@/lib/api/hooks";
import type {
  CaseDetailResponse,
  EvidenceResponse,
  FollowUpResponse,
  InteractionResponse,
  NextActionType,
} from "@/lib/api/types";
import { formatDate, formatDateTime, formatRelativeDue } from "@/lib/format";
import {
  evidenceTypeLabels,
  finalOutcomeLabels,
  interactionTypeLabels,
  outcomeLabels,
} from "@/lib/labels";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { DateWithSource } from "@/components/date-with-source";
import { Money } from "@/components/money";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Input } from "@/components/ui/input";
import { toast } from "sonner";

// ---------------------------------------------------------------------------
// Next step

export const actionCtas: Partial<Record<NextActionType, string>> = {
  AddPurchaseProof: "Add purchase proof",
  SetRequestedOutcome: "Record what you're asking for",
  RecordSellerContact: "Record seller contact",
  RecordUpdate: "Record an update",
  ChangeFollowUp: "Move the follow-up",
  MarkOutcomeReceived: "Mark it received",
  ScheduleFollowUp: "Schedule a follow-up",
  AddEvidence: "Add evidence",
  EditCase: "Edit case",
};

export function NextStepCard({
  issueCase,
  onAction,
}: {
  issueCase: CaseDetailResponse;
  onAction: (action: NextActionType) => void;
}) {
  const dismiss = useDismissNextAction(issueCase.id);
  const na = issueCase.nextAction;
  if (!na) return null;
  const cta = actionCtas[na.actionType];
  const resolved = issueCase.status === "Resolved" || issueCase.status === "Closed";

  return (
    <Card className="border-l-4 border-l-primary">
      <CardHeader className="pb-2">
        <CardTitle className="text-base">{na.title}</CardTitle>
      </CardHeader>
      <CardContent className="space-y-3">
        <p className="text-sm text-muted-foreground">{na.description}</p>
        <div className="flex flex-wrap items-center gap-2">
          {cta && !resolved ? (
            <Button className="touch-target" onClick={() => onAction(na.actionType)}>
              {cta} <ArrowRightIcon aria-hidden />
            </Button>
          ) : null}
          {na.isDismissible && !resolved ? (
            <Button
              variant="ghost"
              className="touch-target"
              onClick={() => dismiss.mutate(na.key)}
              disabled={dismiss.isPending}
            >
              Dismiss
            </Button>
          ) : null}
        </div>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Readiness

const readinessActions: Record<string, { label: string; action: NextActionType }> = {
  PurchaseProof: { label: "Add", action: "AddEvidence" },
  Purchase: { label: "Add", action: "EditCase" },
  SellerContact: { label: "Record", action: "RecordUpdate" },
};

export function ReadinessCard({
  issueCase,
  onAction,
}: {
  issueCase: CaseDetailResponse;
  onAction: (action: NextActionType) => void;
}) {
  const r = issueCase.readiness;
  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Case readiness</CardTitle>
        <p className="text-xs text-muted-foreground">
          {r.completed} of {r.total} details organized
        </p>
      </CardHeader>
      <CardContent className="space-y-3">
        <div className="h-1.5 w-full rounded-full bg-muted" aria-hidden>
          <div
            className="h-1.5 rounded-full bg-primary transition-all"
            style={{ width: `${r.total ? (r.completed / r.total) * 100 : 0}%` }}
          />
        </div>
        <ul className="space-y-2.5">
          {r.items.map((item) => (
            <li key={item.key} className="flex items-start gap-2.5">
              {item.isComplete ? (
                <CheckCircle2Icon className="mt-0.5 size-4 shrink-0 text-success" aria-hidden />
              ) : (
                <CircleIcon className="mt-0.5 size-4 shrink-0 text-muted-foreground/40" aria-hidden />
              )}
              <span className="min-w-0 flex-1">
                <span className="block text-sm">{item.label}</span>
                {!item.isComplete && item.explanation ? (
                  <span className="block text-xs text-muted-foreground">{item.explanation}</span>
                ) : null}
              </span>
              {!item.isComplete && readinessActions[item.key] ? (
                <Button
                  variant="ghost"
                  size="sm"
                  className="touch-target"
                  onClick={() => onAction(readinessActions[item.key].action)}
                >
                  {readinessActions[item.key].label}
                </Button>
              ) : null}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Timeline

const timelineIcons: Record<string, typeof CheckIcon> = {
  CaseCreated: PackageIcon,
  CaseUpdated: PencilIcon,
  StatusChanged: ArrowRightIcon,
  PurchaseLinked: PackageIcon,
  PurchaseProof: PaperclipIcon,
  EvidenceAdded: ImageIcon,
  InteractionRecorded: MessageSquareIcon,
  FollowUpCreated: CalendarClockIcon,
  FollowUpCompleted: CheckCircle2Icon,
  FollowUpCancelled: CalendarClockIcon,
  FollowUpDue: AlertTriangleIcon,
  ReminderCreated: BellIcon,
  ReminderSent: BellIcon,
  ReminderFailed: AlertTriangleIcon,
  CaseResolved: PartyPopperIcon,
  CaseReopened: Undo2Icon,
  CaseClosed: CheckCircle2Icon,
};

const timeFmt = new Intl.DateTimeFormat("en-GB", { hour: "numeric", minute: "2-digit" });

export function TimelineList({ issueCase }: { issueCase: CaseDetailResponse }) {
  const [showAll, setShowAll] = useState(false);
  const events = issueCase.timeline;
  const visible = showAll ? events : events.slice(0, 8);

  // Group consecutive events by calendar day.
  const days: { key: string; date: string; events: typeof visible }[] = [];
  for (const e of visible) {
    const key = new Date(e.occurredAt).toDateString();
    const last = days[days.length - 1];
    if (last && last.key === key) {
      last.events.push(e);
    } else {
      days.push({ key, date: formatDate(e.occurredAt), events: [e] });
    }
  }

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Timeline</CardTitle>
      </CardHeader>
      <CardContent>
        <ol className="space-y-4">
          {days.map((day) => (
            <li key={day.key}>
              <p className="text-xs font-medium text-muted-foreground">{day.date}</p>
              <ol className="mt-1.5 space-y-2.5">
                {day.events.map((e) => {
                  const Icon = timelineIcons[e.eventType] ?? CircleIcon;
                  return (
                    <li key={e.id} className="flex gap-2.5">
                      <Icon className="mt-0.5 size-4 shrink-0 text-muted-foreground" aria-hidden />
                      <div className="min-w-0 flex-1">
                        <p className="text-sm">{e.summary}</p>
                        <p className="text-xs text-muted-foreground">
                          {timeFmt.format(new Date(e.occurredAt))}
                        </p>
                      </div>
                    </li>
                  );
                })}
              </ol>
            </li>
          ))}
        </ol>
        {events.length > 8 ? (
          <Button
            variant="link"
            size="sm"
            className="touch-target mt-2"
            onClick={() => setShowAll((v) => !v)}
          >
            {showAll ? "Show fewer" : `Show all ${events.length} events`}
          </Button>
        ) : null}
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Evidence

export function EvidenceGrid({
  issueCase,
  onAdd,
}: {
  issueCase: CaseDetailResponse;
  onAdd: () => void;
}) {
  const del = useDeleteEvidence(issueCase.id);
  const update = useUpdateEvidence(issueCase.id);
  const [confirming, setConfirming] = useState<EvidenceResponse | null>(null);
  const [renaming, setRenaming] = useState<EvidenceResponse | null>(null);
  const [renameValue, setRenameValue] = useState("");

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between pb-2">
        <CardTitle className="text-base">Evidence</CardTitle>
        <Button variant="outline" size="sm" className="touch-target" onClick={onAdd}>
          <PlusIcon aria-hidden /> Add
        </Button>
      </CardHeader>
      <CardContent>
        {issueCase.evidence.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            Nothing yet. Photos and screenshots make cases stronger.
          </p>
        ) : (
          <ul className="grid grid-cols-2 gap-3 sm:grid-cols-3">
            {issueCase.evidence.map((ev) => (
              <li
                key={ev.id}
                className="relative overflow-hidden rounded-xl border border-border bg-muted/30"
              >
                <a
                  href={`/api/documents/${ev.document.id}`}
                  target="_blank"
                  rel="noopener"
                  className="block rounded-xl transition-colors hover:bg-muted/70"
                  aria-label={`Open ${ev.document.fileName}`}
                >
                  <div className="flex aspect-[4/3] items-center justify-center bg-muted/60">
                    {ev.document.isImage ? (
                      // eslint-disable-next-line @next/next/no-img-element
                      <img
                        src={`/api/documents/${ev.document.id}`}
                        alt=""
                        className="h-full w-full object-cover"
                      />
                    ) : (
                      <FileTextIcon className="size-8 text-muted-foreground" aria-hidden />
                    )}
                  </div>
                  <div className="px-2 py-2 pr-9">
                    <p className="text-[11px] text-muted-foreground">
                      {evidenceTypeLabels[ev.evidenceType]}
                    </p>
                    <p className="truncate text-xs font-medium">
                      {ev.description || ev.document.fileName}
                    </p>
                  </div>
                </a>
                <div className="absolute right-1 bottom-1">
                  <DropdownMenu>
                    <DropdownMenuTrigger
                      render={
                        <Button
                          variant="ghost"
                          size="icon"
                          className="size-9 sm:size-8"
                          aria-label={`Options for ${ev.document.fileName}`}
                        />
                      }
                    >
                      <MoreHorizontalIcon aria-hidden />
                    </DropdownMenuTrigger>
                    <DropdownMenuContent align="end">
                      <DropdownMenuItem
                        render={
                          <a
                            href={`/api/documents/${ev.document.id}`}
                            target="_blank"
                            rel="noopener"
                          />
                        }
                      >
                        Open
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        render={
                          <a
                            href={`/api/documents/${ev.document.id}?download=1`}
                            download={ev.document.fileName}
                          />
                        }
                      >
                        Download
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        onClick={() => {
                          setRenaming(ev);
                          setRenameValue(ev.description ?? "");
                        }}
                      >
                        Rename
                      </DropdownMenuItem>
                      <DropdownMenuItem
                        variant="destructive"
                        onClick={() => setConfirming(ev)}
                      >
                        Remove
                      </DropdownMenuItem>
                    </DropdownMenuContent>
                  </DropdownMenu>
                </div>
              </li>
            ))}
          </ul>
        )}

        <ConfirmDialog
          open={confirming !== null}
          onOpenChange={(o) => !o && setConfirming(null)}
          title="Remove this evidence?"
          description="The file will be permanently deleted."
          confirmLabel="Remove"
          onConfirm={async () => {
            if (confirming) {
              await del.mutateAsync(confirming.id);
              toast.success("Evidence removed.");
            }
          }}
        />

        {renaming ? (
          <div className="mt-3 flex items-end gap-2 rounded-xl border border-border p-3">
            <div className="flex-1">
              <label htmlFor="ev-rename" className="text-xs font-medium">
                Description
              </label>
              <Input
                id="ev-rename"
                value={renameValue}
                onChange={(e) => setRenameValue(e.target.value)}
                maxLength={500}
                className="mt-1"
              />
            </div>
            <Button
              size="sm"
              className="touch-target"
              onClick={async () => {
                await update.mutateAsync({
                  evidenceId: renaming.id,
                  evidenceType: renaming.evidenceType,
                  description: renameValue.trim() || null,
                });
                setRenaming(null);
                toast.success("Saved.");
              }}
            >
              Save
            </Button>
            <Button
              size="sm"
              variant="ghost"
              className="touch-target"
              onClick={() => setRenaming(null)}
            >
              Cancel
            </Button>
          </div>
        ) : null}
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Interactions

export function InteractionsList({
  issueCase,
  onAdd,
  onEdit,
}: {
  issueCase: CaseDetailResponse;
  onAdd: () => void;
  onEdit: (interaction: InteractionResponse) => void;
}) {
  const del = useDeleteInteraction(issueCase.id);
  const [confirming, setConfirming] = useState<InteractionResponse | null>(null);

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between pb-2">
        <CardTitle className="text-base">Seller interactions</CardTitle>
        <Button variant="outline" size="sm" className="touch-target" onClick={onAdd}>
          <MessageSquareIcon aria-hidden /> Record update
        </Button>
      </CardHeader>
      <CardContent>
        {issueCase.interactions.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No updates recorded yet. Each time you or the seller does something,
            note it here.
          </p>
        ) : (
          <ul className="space-y-3">
            {issueCase.interactions.map((it) => (
              <li key={it.id} className="rounded-xl border border-border p-3">
                <div className="flex items-start justify-between gap-2">
                  <div className="min-w-0">
                    <p className="text-sm font-medium">
                      {interactionTypeLabels[it.interactionType]}
                    </p>
                    <p className="text-xs text-muted-foreground">
                      {formatDateTime(it.occurredAt)}
                    </p>
                  </div>
                  <div className="flex shrink-0 gap-1">
                    <Button
                      variant="ghost"
                      size="icon"
                      className="size-11 sm:size-9"
                      aria-label="Edit update"
                      onClick={() => onEdit(it)}
                    >
                      <PencilIcon aria-hidden />
                    </Button>
                    <Button
                      variant="ghost"
                      size="icon"
                      className="size-11 text-destructive sm:size-9"
                      aria-label="Delete update"
                      onClick={() => setConfirming(it)}
                    >
                      <Trash2Icon aria-hidden />
                    </Button>
                  </div>
                </div>
                <p className="mt-1.5 whitespace-pre-wrap text-sm">{it.note}</p>
                {it.document ? (
                  <a
                    href={`/api/documents/${it.document.id}`}
                    target="_blank"
                    rel="noopener"
                    className="mt-2 inline-flex items-center gap-1.5 rounded-full border border-border px-2.5 py-1 text-xs text-muted-foreground hover:border-primary/50"
                  >
                    <PaperclipIcon className="size-3" aria-hidden />
                    {it.document.fileName}
                  </a>
                ) : null}
              </li>
            ))}
          </ul>
        )}
        <ConfirmDialog
          open={confirming !== null}
          onOpenChange={(o) => !o && setConfirming(null)}
          title="Delete this update?"
          description="The update and its attachment will be permanently removed."
          confirmLabel="Delete"
          onConfirm={async () => {
            if (confirming) {
              await del.mutateAsync(confirming.id);
              toast.success("Update deleted.");
            }
          }}
        />
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Follow-ups

export function FollowUpsList({
  issueCase,
  onSchedule,
  onMove,
}: {
  issueCase: CaseDetailResponse;
  onSchedule: () => void;
  onMove: (followUp: FollowUpResponse) => void;
}) {
  const complete = useCompleteFollowUp(issueCase.id);
  const cancel = useCancelFollowUp(issueCase.id);
  const [confirming, setConfirming] = useState<FollowUpResponse | null>(null);
  const [showEarlier, setShowEarlier] = useState(false);

  const isOpen = (f: FollowUpResponse) => !f.completedAt && !f.cancelledAt;
  const open = issueCase.followUps.filter(isOpen);
  const earlier = issueCase.followUps.filter((f) => !isOpen(f));

  return (
    <Card>
      <CardHeader className="flex-row items-center justify-between pb-2">
        <CardTitle className="text-base">Follow-ups</CardTitle>
        <Button variant="outline" size="sm" className="touch-target" onClick={onSchedule}>
          <CalendarClockIcon aria-hidden /> Schedule
        </Button>
      </CardHeader>
      <CardContent className="space-y-3">
        {open.length === 0 && earlier.length === 0 ? (
          <p className="text-sm text-muted-foreground">
            No follow-ups. Schedule one so you don&apos;t have to keep checking.
          </p>
        ) : (
          <ul className="space-y-2">
            {open.map((fu) => (
              <li
                key={fu.id}
                className="flex flex-col gap-2 rounded-xl border border-border p-3 sm:flex-row sm:items-center"
              >
                <div className="min-w-0 flex-1">
                  <p className="text-sm font-medium">{fu.title}</p>
                  <p className="text-xs text-muted-foreground">
                    {formatRelativeDue(fu.dueAt)} · {formatDateTime(fu.dueAt)}
                  </p>
                </div>
                <div className="flex gap-1.5 max-sm:[&>*]:flex-1 sm:shrink-0">
                  <Button
                    size="sm"
                    variant="outline"
                    className="touch-target"
                    onClick={async () => {
                      await complete.mutateAsync(fu.id);
                      toast.success("Marked done.");
                    }}
                  >
                    <CheckIcon aria-hidden /> Done
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="touch-target"
                    onClick={() => onMove(fu)}
                  >
                    Move
                  </Button>
                  <Button
                    size="sm"
                    variant="ghost"
                    className="touch-target text-muted-foreground"
                    onClick={() => setConfirming(fu)}
                  >
                    Cancel
                  </Button>
                </div>
              </li>
            ))}
          </ul>
        )}
        {earlier.length > 0 ? (
          <div>
            <Button
              variant="link"
              size="sm"
              className="touch-target"
              onClick={() => setShowEarlier((v) => !v)}
            >
              {showEarlier ? "Hide earlier" : "Show earlier"}
            </Button>
            {showEarlier ? (
              <ul className="mt-1 space-y-1.5">
                {earlier.map((fu) => (
                  <li key={fu.id} className="text-xs text-muted-foreground">
                    {fu.title} · {formatDate(fu.dueAt)} ·{" "}
                    {fu.completedAt ? "Done" : "Cancelled"}
                  </li>
                ))}
              </ul>
            ) : null}
          </div>
        ) : null}
        <ConfirmDialog
          open={confirming !== null}
          onOpenChange={(o) => !o && setConfirming(null)}
          title="Cancel this follow-up?"
          description="You won't be reminded about it."
          confirmLabel="Cancel follow-up"
          onConfirm={async () => {
            if (confirming) {
              await cancel.mutateAsync(confirming.id);
              toast.success("Follow-up cancelled.");
            }
          }}
        />
      </CardContent>
    </Card>
  );
}

// ---------------------------------------------------------------------------
// Summary

export function SummaryCard({ issueCase }: { issueCase: CaseDetailResponse }) {
  const p = issueCase.purchase;
  const next = issueCase.followUps.find((f) => !f.completedAt && !f.cancelledAt);
  const { data: reminders } = useCaseReminders(issueCase.id);
  const failedReminder = (reminders ?? []).some((r) => r.status === "Failed");
  const affected = (p?.items ?? []).filter((i) => issueCase.affectedItemIds.includes(i.id));
  const deadlineItems = affected.length > 0 ? affected : (p?.items ?? []);

  return (
    <Card>
      <CardHeader className="pb-2">
        <CardTitle className="text-base">Summary</CardTitle>
      </CardHeader>
      <CardContent className="space-y-4 text-sm">
        {p ? (
          <dl className="space-y-1.5">
            <dt className="text-xs font-medium text-muted-foreground">Purchase</dt>
            <dd>
              <Link
                href={`/app/purchases/${p.id}`}
                className="font-medium underline-offset-4 hover:underline"
              >
                {p.merchantName}
              </Link>
            </dd>
            {p.orderNumber ? (
              <dd className="font-mono text-xs text-muted-foreground">{p.orderNumber}</dd>
            ) : null}
            {p.purchaseDate ? (
              <dd className="text-xs text-muted-foreground">{formatDate(p.purchaseDate)}</dd>
            ) : null}
            {p.totalAmount !== null ? (
              <dd>
                <Money amount={p.totalAmount} currency={p.currency} className="font-medium" />
              </dd>
            ) : null}
          </dl>
        ) : (
          <p className="text-xs text-muted-foreground">No purchase linked yet.</p>
        )}

        {affected.length > 0 ? (
          <div>
            <p className="text-xs font-medium text-muted-foreground">Affected items</p>
            <p className="mt-0.5">{affected.map((i) => i.productName).join(", ")}</p>
          </div>
        ) : null}

        <div>
          <p className="text-xs font-medium text-muted-foreground">Requested outcome</p>
          <p className="mt-0.5">
            {outcomeLabels[issueCase.requestedOutcomeType]}
            {issueCase.requestedAmount !== null ? (
              <>
                {" — "}
                <Money amount={issueCase.requestedAmount} currency={issueCase.requestedCurrency} />
              </>
            ) : null}
          </p>
        </div>

        {next ? (
          <div>
            <p className="text-xs font-medium text-muted-foreground">Next follow-up</p>
            <p className="mt-0.5">
              {next.title} · {formatRelativeDue(next.dueAt)}
            </p>
          </div>
        ) : null}

        <dl className="space-y-1.5">
          <dt className="text-xs font-medium text-muted-foreground">Key dates</dt>
          {issueCase.outcomePromisedAt ? (
            <dd className="text-xs">
              Promised on {formatDate(issueCase.outcomePromisedAt)}
            </dd>
          ) : null}
          {issueCase.outcomeExpectedBy ? (
            <dd className="text-xs">
              Expected by {formatDate(issueCase.outcomeExpectedBy)}
            </dd>
          ) : null}
          {deadlineItems.map((item) =>
            item.returnDeadline ? (
              <dd key={`rd-${item.id}`} className="flex flex-wrap items-baseline gap-1 text-xs">
                <span>Return deadline:</span>
                <DateWithSource
                  date={item.returnDeadline}
                  provenance={item.returnDeadlineProvenance}
                />
              </dd>
            ) : item.commercialWarrantyEnd ? (
              <dd key={`wd-${item.id}`} className="flex flex-wrap items-baseline gap-1 text-xs">
                <span>Warranty until:</span>
                <DateWithSource
                  date={item.commercialWarrantyEnd}
                  provenance={item.commercialWarrantyEndProvenance}
                />
              </dd>
            ) : null,
          )}
        </dl>

        {issueCase.finalOutcome ? (
          <div className="rounded-xl bg-success/10 p-3">
            <p className="text-xs font-medium text-success">Outcome</p>
            <p className="mt-0.5 text-sm">
              {finalOutcomeLabels[issueCase.finalOutcome.type]}
              {issueCase.finalOutcome.amount !== null ? (
                <>
                  {" — "}
                  <Money
                    amount={issueCase.finalOutcome.amount}
                    currency={issueCase.finalOutcome.currency}
                  />
                </>
              ) : null}
            </p>
          </div>
        ) : null}

        {failedReminder ? (
          <p className="rounded-xl bg-warning/10 px-3 py-2 text-xs text-warning">
            We couldn&apos;t send a reminder email.{" "}
            <Link href="/app/profile" className="underline underline-offset-2">
              Check your preferences
            </Link>
          </p>
        ) : null}
      </CardContent>
    </Card>
  );
}
