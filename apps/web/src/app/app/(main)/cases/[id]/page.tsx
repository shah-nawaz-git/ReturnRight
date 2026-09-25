"use client";

import {
  ArrowLeftIcon,
  CalendarClockIcon,
  CheckCircle2Icon,
  ChevronDownIcon,
  DownloadIcon,
  EllipsisIcon,
  FilePlusIcon,
  MessageSquarePlusIcon,
  PencilIcon,
  SearchXIcon,
  Trash2Icon,
  Undo2Icon,
} from "lucide-react";
import Link from "next/link";
import { useParams, useRouter, useSearchParams } from "next/navigation";
import { Suspense, useEffect, useState } from "react";
import { toast } from "sonner";

import { ApiError } from "@/lib/api/client";
import { useCase, useDeleteCase, useReopenCase } from "@/lib/api/hooks";
import type { InteractionResponse, FollowUpResponse, NextActionType } from "@/lib/api/types";
import { formatMoney, formatRelativeDue } from "@/lib/format";
import { finalOutcomeLabels, outcomeLabels, problemTypeLabels } from "@/lib/labels";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { EmptyState } from "@/components/empty-state";
import { InlineError } from "@/components/inline-error";
import { StatusBadge } from "@/components/status-badge";
import {
  AddEvidenceDialog,
  ChangeStatusDialog,
  EditCaseDialog,
  RecordUpdateDialog,
  ResolveDialog,
  ScheduleFollowUpDialog,
} from "@/components/case-workspace/dialogs";
import {
  EvidenceGrid,
  FollowUpsList,
  InteractionsList,
  NextStepCard,
  ReadinessCard,
  SummaryCard,
  TimelineList,
} from "@/components/case-workspace/panels";
import { Button } from "@/components/ui/button";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Skeleton } from "@/components/ui/skeleton";

function CaseWorkspace() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const searchParams = useSearchParams();
  const id = params.id;
  const { data: issueCase, isLoading, error, refetch } = useCase(id);
  const reopen = useReopenCase(id);
  const deleteCase = useDeleteCase(id);

  const [recordOpen, setRecordOpen] = useState(false);
  const [editInteraction, setEditInteraction] = useState<InteractionResponse | null>(null);
  const [evidenceOpen, setEvidenceOpen] = useState(false);
  const [evidenceType, setEvidenceType] = useState<"DamagePhoto" | "PurchaseProof">("DamagePhoto");
  const [followUpOpen, setFollowUpOpen] = useState(false);
  const [moveFollowUp, setMoveFollowUp] = useState<FollowUpResponse | null>(null);
  const [statusOpen, setStatusOpen] = useState(false);
  const [resolveOpen, setResolveOpen] = useState(false);
  const [editCaseOpen, setEditCaseOpen] = useState(false);
  const [deleteOpen, setDeleteOpen] = useState(false);

  useEffect(() => {
    if (searchParams.get("created") === "1") {
      toast.success("Your case is ready.");
      router.replace(`/app/cases/${id}`);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  if (isLoading) {
    return (
      <div className="space-y-4" aria-busy="true">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-36 rounded-2xl" />
        <Skeleton className="h-52 rounded-2xl" />
      </div>
    );
  }

  if (error || !issueCase) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <EmptyState
          icon={SearchXIcon}
          title="This case doesn't exist or was deleted."
          description="It may have been removed, or the link is wrong."
        >
          <Button onClick={() => router.push("/app/cases")}>Back to my cases</Button>
        </EmptyState>
      );
    }
    return <InlineError error={error} onRetry={() => refetch()} />;
  }

  const resolved = issueCase.status === "Resolved" || issueCase.status === "Closed";

  const runAction = (action: NextActionType) => {
    switch (action) {
      case "AddPurchaseProof":
        setEvidenceType("PurchaseProof");
        setEvidenceOpen(true);
        break;
      case "SetRequestedOutcome":
      case "EditCase":
        setEditCaseOpen(true);
        break;
      case "RecordSellerContact":
      case "RecordUpdate":
        setEditInteraction(null);
        setRecordOpen(true);
        break;
      case "ChangeFollowUp":
      case "ScheduleFollowUp": {
        const fu = issueCase.followUps.find(
          (f) => f.id === issueCase.nextAction.followUpId,
        );
        if (fu) {
          setMoveFollowUp(fu);
          setFollowUpOpen(true);
        } else {
          setMoveFollowUp(null);
          setFollowUpOpen(true);
        }
        break;
      }
      case "MarkOutcomeReceived":
        setResolveOpen(true);
        break;
      case "AddEvidence":
        setEvidenceType("DamagePhoto");
        setEvidenceOpen(true);
        break;
      case "None":
        break;
    }
  };

  const menuItems = (
    <>
      <DropdownMenuItem
        onClick={() => {
          setMoveFollowUp(null);
          setFollowUpOpen(true);
        }}
        disabled={resolved}
      >
        <CalendarClockIcon aria-hidden /> Schedule follow-up
      </DropdownMenuItem>
      <DropdownMenuItem onClick={() => setStatusOpen(true)} disabled={resolved}>
        <CheckCircle2Icon aria-hidden /> Change status
      </DropdownMenuItem>
      {resolved ? (
        <DropdownMenuItem
          onClick={async () => {
            await reopen.mutateAsync();
            toast.success("Case reopened.");
          }}
          disabled={issueCase.status === "Closed"}
        >
          <Undo2Icon aria-hidden /> Reopen
        </DropdownMenuItem>
      ) : (
        <DropdownMenuItem onClick={() => setResolveOpen(true)}>
          <CheckCircle2Icon aria-hidden /> Resolve case
        </DropdownMenuItem>
      )}
      <DropdownMenuItem onClick={() => setEditCaseOpen(true)} disabled={resolved}>
        <PencilIcon aria-hidden /> Edit case
      </DropdownMenuItem>
      <DropdownMenuItem
        render={<a href={`/api/cases/${id}/case-file`} download />}
      >
        <DownloadIcon aria-hidden /> Download Case File
      </DropdownMenuItem>
      <DropdownMenuItem
        variant="destructive"
        onClick={() => setDeleteOpen(true)}
      >
        <Trash2Icon aria-hidden /> Delete case
      </DropdownMenuItem>
    </>
  );

  return (
    <div className="space-y-5">
      <nav aria-label="Breadcrumb">
        <Link
          href="/app/cases"
          className="inline-flex min-h-11 items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeftIcon className="size-4" aria-hidden /> My cases
        </Link>
      </nav>

      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight">{issueCase.title}</h1>
          <p className="mt-0.5 text-sm text-muted-foreground">
            {issueCase.purchase?.merchantName
              ? `${issueCase.purchase.merchantName} · `
              : ""}
            {problemTypeLabels[issueCase.problemType]}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <StatusBadge status={issueCase.status} />
          {!resolved ? (
            <>
              <Button
                variant="outline"
                className="touch-target hidden sm:inline-flex"
                onClick={() => {
                  setEvidenceType("DamagePhoto");
                  setEvidenceOpen(true);
                }}
              >
                <FilePlusIcon aria-hidden /> Add evidence
              </Button>
              <Button
                className="touch-target"
                onClick={() => {
                  setEditInteraction(null);
                  setRecordOpen(true);
                }}
              >
                <MessageSquarePlusIcon aria-hidden /> Record update
              </Button>
            </>
          ) : null}
          <DropdownMenu>
            <DropdownMenuTrigger
              render={
                <Button
                  variant="outline"
                  size="icon"
                  className="touch-target"
                  aria-label="More actions"
                />
              }
            >
              <EllipsisIcon aria-hidden />
            </DropdownMenuTrigger>
            <DropdownMenuContent align="end">{menuItems}</DropdownMenuContent>
          </DropdownMenu>
        </div>
      </header>

      <details className="group rounded-2xl border border-border bg-card lg:hidden">
        <summary className="flex min-h-11 cursor-pointer list-none items-center gap-2 px-4 py-3 text-sm marker:hidden">
          <span className="min-w-0 flex-1">
            <span className="block truncate font-medium">
              {[
                issueCase.purchase?.merchantName,
                issueCase.purchase?.orderNumber
                  ? `Order ${issueCase.purchase.orderNumber}`
                  : null,
              ]
                .filter(Boolean)
                .join(" · ") || "Purchase & request"}
            </span>
            <span className="block truncate text-xs text-muted-foreground">
              {[
                `${outcomeLabels[issueCase.requestedOutcomeType]}${issueCase.requestedAmount !== null ? ` ${formatMoney(issueCase.requestedAmount, issueCase.requestedCurrency)}` : ""}`,
                (() => {
                  const next = issueCase.followUps.find(
                    (f) => !f.completedAt && !f.cancelledAt,
                  );
                  return next ? `Next follow-up: ${formatRelativeDue(next.dueAt)}` : null;
                })(),
              ]
                .filter(Boolean)
                .join(" · ")}
            </span>
          </span>
          <ChevronDownIcon
            className="size-4 shrink-0 text-muted-foreground transition-transform group-open:rotate-180"
            aria-hidden
          />
        </summary>
        <div className="border-t border-border">
          <SummaryCard issueCase={issueCase} />
        </div>
      </details>

      {resolved ? (
        <div
          className="flex flex-wrap items-center justify-between gap-3 rounded-2xl bg-success/10 px-4 py-3"
          role="status"
        >
          <p className="flex items-center gap-2 text-sm font-medium text-success">
            <CheckCircle2Icon className="size-4" aria-hidden />
            {issueCase.finalOutcome
              ? `Resolved — ${finalOutcomeLabels[issueCase.finalOutcome.type]}`
              : "This case is resolved."}
          </p>
          <div className="flex gap-2">
            <Button
              variant="outline"
              size="sm"
              className="touch-target"
              render={<a href={`/api/cases/${id}/case-file`} download />}
            >
              <DownloadIcon aria-hidden /> Case File
            </Button>
            <Button
              variant="ghost"
              size="sm"
              className="touch-target"
              onClick={async () => {
                await reopen.mutateAsync();
                toast.success("Case reopened.");
              }}
              disabled={reopen.isPending || issueCase.status === "Closed"}
            >
              Reopen
            </Button>
          </div>
        </div>
      ) : null}

      <div className="grid gap-5 lg:grid-cols-[2fr_1fr]">
        <div className="space-y-5">
          <NextStepCard issueCase={issueCase} onAction={runAction} />
          <ReadinessCard issueCase={issueCase} onAction={runAction} />
          <TimelineList issueCase={issueCase} />
          <EvidenceGrid
            issueCase={issueCase}
            onAdd={() => {
              setEvidenceType("DamagePhoto");
              setEvidenceOpen(true);
            }}
          />
          <InteractionsList
            issueCase={issueCase}
            onAdd={() => {
              setEditInteraction(null);
              setRecordOpen(true);
            }}
            onEdit={(it) => {
              setEditInteraction(it);
              setRecordOpen(true);
            }}
          />
          <FollowUpsList
            issueCase={issueCase}
            onSchedule={() => {
              setMoveFollowUp(null);
              setFollowUpOpen(true);
            }}
            onMove={(fu) => {
              setMoveFollowUp(fu);
              setFollowUpOpen(true);
            }}
          />
        </div>
        <div className="hidden lg:sticky lg:top-4 lg:block lg:self-start">
          <SummaryCard issueCase={issueCase} />
          <Button
            variant="outline"
            className="touch-target mt-3 w-full"
            render={<a href={`/api/cases/${id}/case-file`} download />}
          >
            <DownloadIcon aria-hidden /> Download Case File
          </Button>
        </div>
      </div>

      {recordOpen ? (
        <RecordUpdateDialog
          caseId={id}
          interaction={editInteraction}
          open
          onOpenChange={setRecordOpen}
        />
      ) : null}
      {evidenceOpen ? (
        <AddEvidenceDialog
          caseId={id}
          initialType={evidenceType}
          open
          onOpenChange={setEvidenceOpen}
        />
      ) : null}
      {followUpOpen ? (
        <ScheduleFollowUpDialog
          caseId={id}
          followUp={moveFollowUp}
          open
          onOpenChange={(o) => {
            setFollowUpOpen(o);
            if (!o) setMoveFollowUp(null);
          }}
        />
      ) : null}
      {statusOpen ? (
        <ChangeStatusDialog issueCase={issueCase} open onOpenChange={setStatusOpen} />
      ) : null}
      {resolveOpen ? (
        <ResolveDialog issueCase={issueCase} open onOpenChange={setResolveOpen} />
      ) : null}
      {editCaseOpen ? (
        <EditCaseDialog issueCase={issueCase} open onOpenChange={setEditCaseOpen} />
      ) : null}
      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete this case?"
        description="This permanently removes the case, its timeline, evidence files and follow-ups. Linked purchases and documents stay."
        confirmLabel="Delete case"
        onConfirm={async () => {
          await deleteCase.mutateAsync();
          toast.success("Case deleted.");
          router.push("/app/cases");
        }}
      />
    </div>
  );
}

export default function CasePage() {
  return (
    <Suspense>
      <CaseWorkspace />
    </Suspense>
  );
}
