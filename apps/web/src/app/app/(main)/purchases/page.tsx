"use client";

import { FileCheck2Icon, FileX2Icon, PackageIcon, PlusIcon } from "lucide-react";
import Link from "next/link";
import { useRouter } from "next/navigation";

import { usePurchases } from "@/lib/api/hooks";
import { formatDate } from "@/lib/format";
import { EmptyState } from "@/components/empty-state";
import { InlineError } from "@/components/inline-error";
import { Money } from "@/components/money";
import { PageHeader } from "@/components/page-header";
import { Badge } from "@/components/ui/badge";
import { Button } from "@/components/ui/button";
import { Card } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

export default function PurchasesPage() {
  const router = useRouter();
  const { data: purchases, isLoading, error, refetch } = usePurchases();

  return (
    <div className="space-y-5">
      <PageHeader
        title="Purchases"
        description="Saved purchases are ready to use if something goes wrong."
        actions={
          <Button className="touch-target" onClick={() => router.push("/app/purchases/new")}>
            <PlusIcon aria-hidden /> Save a purchase
          </Button>
        }
      />

      {isLoading ? (
        <div className="grid gap-3 sm:grid-cols-2">
          {[0, 1, 2].map((i) => (
            <Skeleton key={i} className="h-32 rounded-2xl" />
          ))}
        </div>
      ) : error ? (
        <InlineError error={error} onRetry={() => refetch()} />
      ) : !purchases || purchases.length === 0 ? (
        <EmptyState
          icon={PackageIcon}
          title="No purchases yet"
          description="Save a purchase now so it's ready if something goes wrong later. It's optional — you can also add details when you start a case."
        >
          <Button onClick={() => router.push("/app/purchases/new")}>
            <PlusIcon aria-hidden /> Save a purchase
          </Button>
        </EmptyState>
      ) : (
        <ul className="grid gap-3 sm:grid-cols-2">
          {purchases.map((p) => (
            <li key={p.id}>
              <Card className="h-full p-0">
                <Link
                  href={`/app/purchases/${p.id}`}
                  className="block rounded-xl p-4 transition-colors hover:bg-muted/50"
                >
                  <div className="flex items-start justify-between gap-2">
                    <div className="min-w-0">
                      <p className="truncate font-medium">{p.merchantName}</p>
                      <p className="mt-0.5 truncate text-sm text-muted-foreground">
                        {p.firstItemName
                          ? p.itemCount > 1
                            ? `${p.firstItemName} +${p.itemCount - 1} more`
                            : p.firstItemName
                          : "No items listed"}
                      </p>
                    </div>
                    <Badge
                      variant="outline"
                      className={
                        p.hasProof
                          ? "border-success/30 bg-success/10 text-success"
                          : "text-muted-foreground"
                      }
                    >
                      {p.hasProof ? (
                        <>
                          <FileCheck2Icon data-icon="inline-start" aria-hidden /> Receipt saved
                        </>
                      ) : (
                        <>
                          <FileX2Icon data-icon="inline-start" aria-hidden /> No proof
                        </>
                      )}
                    </Badge>
                  </div>
                  <div className="mt-3 flex items-baseline justify-between text-sm">
                    <span className="text-muted-foreground">
                      {p.purchaseDate ? formatDate(p.purchaseDate) : "No date"}
                    </span>
                    <Money amount={p.totalAmount} currency={p.currency} className="font-medium" />
                  </div>
                  {p.activeCaseCount > 0 ? (
                    <p className="mt-2 text-xs font-medium text-primary">
                      {p.activeCaseCount} active {p.activeCaseCount === 1 ? "case" : "cases"}
                    </p>
                  ) : null}
                </Link>
              </Card>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
