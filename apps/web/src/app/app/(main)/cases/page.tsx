"use client";

import { BriefcaseIcon, CheckCircle2Icon, PlusIcon } from "lucide-react";
import Link from "next/link";
import { useRouter, useSearchParams } from "next/navigation";
import { Suspense } from "react";

import { useCases } from "@/lib/api/hooks";
import { CaseCard } from "@/components/case-card";
import { EmptyState } from "@/components/empty-state";
import { InlineError } from "@/components/inline-error";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";
import { Tabs, TabsList, TabsTrigger } from "@/components/ui/tabs";

function CaseList({ filter }: { filter: "active" | "resolved" }) {
  const { data: cases, isPending, error, refetch } = useCases(filter);

  if (isPending) {
    return (
      <div className="space-y-2">
        {[0, 1, 2].map((i) => (
          <Skeleton key={i} className="h-20 w-full rounded-xl" />
        ))}
      </div>
    );
  }
  if (error) {
    return <InlineError error={error} onRetry={() => refetch()} />;
  }
  if (!cases || cases.length === 0) {
    return filter === "active" ? (
      <EmptyState
        icon={BriefcaseIcon}
        title="No active cases"
        description="If something has gone wrong with a purchase, create a case and keep everything in one place."
      >
        <Button nativeButton={false} render={<Link href="/app/cases/new" />} className="touch-target">
          <PlusIcon aria-hidden /> Start a case
        </Button>
      </EmptyState>
    ) : (
      <EmptyState
        icon={CheckCircle2Icon}
        title="No resolved cases yet"
        description="When you resolve a case it moves here, so you can look back at how it went."
      />
    );
  }
  return (
    <div className="space-y-2">
      {cases.map((c) => (
        <CaseCard key={c.id} issueCase={c} />
      ))}
    </div>
  );
}

function CasesInner() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const filter = searchParams.get("filter") === "resolved" ? "resolved" : "active";

  return (
    <div>
      <PageHeader
        title="My cases"
        actions={
          <Button nativeButton={false} render={<Link href="/app/cases/new" />} className="touch-target max-sm:hidden">
            <PlusIcon aria-hidden /> Start a case
          </Button>
        }
      />
      <Tabs
        value={filter}
        onValueChange={(v) => router.replace(`/app/cases?filter=${v}`)}
        className="mb-4"
      >
        <TabsList>
          <TabsTrigger value="active">Active</TabsTrigger>
          <TabsTrigger value="resolved">Resolved</TabsTrigger>
        </TabsList>
      </Tabs>
      <CaseList filter={filter} />
    </div>
  );
}

export default function CasesPage() {
  return (
    <Suspense>
      <CasesInner />
    </Suspense>
  );
}
