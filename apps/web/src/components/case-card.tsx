import { ChevronRightIcon } from "lucide-react";
import Link from "next/link";

import { formatRelativeDue, formatRelativeUpdated } from "@/lib/format";
import { outcomeLabels, problemTypeLabels } from "@/lib/labels";
import type { CaseSummaryResponse } from "@/lib/api/types";
import { StatusBadge } from "@/components/status-badge";

export function CaseCard({ issueCase }: { issueCase: CaseSummaryResponse }) {
  return (
    <Link
      href={`/app/cases/${issueCase.id}`}
      className="flex items-center gap-3 rounded-xl border border-border bg-card px-4 py-3 transition-colors hover:border-primary/40 focus-visible:outline-2 focus-visible:outline-ring"
    >
      <div className="min-w-0 flex-1">
        <div className="flex flex-wrap items-center gap-x-2 gap-y-1">
          <h3 className="truncate text-sm font-semibold">{issueCase.title}</h3>
          <StatusBadge status={issueCase.status} />
        </div>
        <p className="mt-0.5 truncate text-xs text-muted-foreground">
          {[issueCase.merchantName, problemTypeLabels[issueCase.problemType]]
            .filter(Boolean)
            .join(" · ")}
          {" · "}
          {outcomeLabels[issueCase.requestedOutcomeType]}
        </p>
        <p className="mt-1 text-xs">
          {issueCase.status !== "Resolved" && issueCase.status !== "Closed" ? (
            <>
              {issueCase.hasOverdueFollowUp ? (
                <span className="font-medium text-destructive">
                  {issueCase.nextFollowUpAt
                    ? formatRelativeDue(issueCase.nextFollowUpAt)
                    : "Follow-up overdue"}
                </span>
              ) : issueCase.nextFollowUpAt ? (
                <span className="text-muted-foreground">
                  {formatRelativeDue(issueCase.nextFollowUpAt)}
                </span>
              ) : null}
              <span className="text-muted-foreground"> · </span>
            </>
          ) : null}
          <span className="text-muted-foreground">
            Updated {formatRelativeUpdated(issueCase.updatedAt)}
          </span>
        </p>
      </div>
      <ChevronRightIcon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
    </Link>
  );
}
