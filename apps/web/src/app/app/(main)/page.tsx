"use client";

import { CalendarClockIcon, PackagePlusIcon, PlusIcon, TriangleAlertIcon } from "lucide-react";
import Link from "next/link";

import { useHome } from "@/lib/api/hooks";
import { formatRelativeDue } from "@/lib/format";
import { CaseCard } from "@/components/case-card";
import { EmptyState } from "@/components/empty-state";
import { InlineError } from "@/components/inline-error";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { BriefcaseIcon } from "lucide-react";

function HomeSkeleton() {
  return (
    <div className="space-y-3">
      {[0, 1, 2].map((i) => (
        <Skeleton key={i} className="h-20 w-full rounded-xl" />
      ))}
    </div>
  );
}

export default function AppHome() {
  const { data: home, isPending, error, refetch } = useHome();

  if (isPending) {
    return (
      <div>
        <PageHeader title="Overview" />
        <HomeSkeleton />
      </div>
    );
  }

  if (error || !home) {
    return (
      <div>
        <PageHeader title="Overview" />
        <InlineError error={error} onRetry={() => refetch()} />
      </div>
    );
  }

  const stats: string[] = [];
  if (home.activeCaseCount > 0) {
    stats.push(`${home.activeCaseCount} active case${home.activeCaseCount === 1 ? "" : "s"}`);
  }
  const upcoming = home.upcomingFollowUps.length;
  if (upcoming > 0) {
    stats.push(`${upcoming} follow-up${upcoming === 1 ? "" : "s"} in the next 7 days`);
  }
  if (home.followUpsDueTodayCount > 0) {
    stats.push(`${home.followUpsDueTodayCount} due today`);
  }
  if (home.overdueFollowUpCount > 0) {
    stats.push(`${home.overdueFollowUpCount} overdue`);
  }

  if (home.activeCaseCount === 0 && home.recentCases.length === 0) {
    return (
      <div>
        <PageHeader title="Overview" />
        <EmptyState
          icon={BriefcaseIcon}
          title="No cases yet"
          description="If something has gone wrong with a purchase, create a case and keep everything in one place."
        >
          <Button nativeButton={false} render={<Link href="/app/cases/new" />} className="touch-target">
            <PlusIcon aria-hidden /> Start a case
          </Button>
          <Button variant="outline" nativeButton={false} render={<Link href="/app/purchases/new" />} className="touch-target">
            <PackagePlusIcon aria-hidden /> Save a purchase
          </Button>
        </EmptyState>
      </div>
    );
  }

  return (
    <div className="space-y-8">
      <PageHeader
        title="Overview"
        actions={
          <Button nativeButton={false} render={<Link href="/app/cases/new" />} className="touch-target max-sm:hidden">
            <PlusIcon aria-hidden /> Start a case
          </Button>
        }
      />

      {stats.length > 0 ? (
        <p className="-mt-4 text-sm text-muted-foreground">{stats.join(" · ")}</p>
      ) : null}

      {home.attention.length > 0 ? (
        <section aria-labelledby="needs-attention">
          <h2 id="needs-attention" className="mb-3 flex items-center gap-2 text-base font-semibold">
            <TriangleAlertIcon className="size-4 text-warning" aria-hidden />
            Needs attention
          </h2>
          <div className="space-y-2">
            {home.attention.map((c) => (
              <div key={c.id}>
                <CaseCard issueCase={c} />
                <p className="mt-1 pl-4 text-xs text-destructive">
                  {c.hasOverdueFollowUp
                    ? c.nextFollowUpAt
                      ? formatRelativeDue(c.nextFollowUpAt)
                      : "Follow-up overdue"
                    : c.outcomeExpectedBy
                      ? "The seller's expected date has passed"
                      : null}
                </p>
              </div>
            ))}
          </div>
        </section>
      ) : null}

      {home.upcomingFollowUps.length > 0 ? (
        <section aria-labelledby="upcoming">
          <h2 id="upcoming" className="mb-3 flex items-center gap-2 text-base font-semibold">
            <CalendarClockIcon className="size-4 text-muted-foreground" aria-hidden />
            Upcoming follow-ups
          </h2>
          <ul className="space-y-2">
            {home.upcomingFollowUps.map((f) => (
              <li key={f.followUpId}>
                <Link
                  href={`/app/cases/${f.caseId}`}
                  className="flex items-center justify-between gap-3 rounded-xl border border-border bg-card px-4 py-3 hover:border-primary/40"
                >
                  <div className="min-w-0">
                    <p className="truncate text-sm font-medium">{f.title}</p>
                    <p className="truncate text-xs text-muted-foreground">{f.caseTitle}</p>
                  </div>
                  <span className="shrink-0 text-xs font-medium text-muted-foreground">
                    {formatRelativeDue(f.dueAt)}
                  </span>
                </Link>
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {home.recentCases.length > 0 ? (
        <section aria-labelledby="recent">
          <h2 id="recent" className="mb-3 text-base font-semibold">Recent cases</h2>
          <div className="space-y-2">
            {home.recentCases.map((c) => (
              <CaseCard key={c.id} issueCase={c} />
            ))}
          </div>
        </section>
      ) : null}
    </div>
  );
}
