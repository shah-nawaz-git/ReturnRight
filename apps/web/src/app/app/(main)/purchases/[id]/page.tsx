"use client";

import {
  ArrowLeftIcon,
  DownloadIcon,
  FileTextIcon,
  Loader2Icon,
  PencilIcon,
  PlusIcon,
  SearchXIcon,
  Trash2Icon,
} from "lucide-react";
import Link from "next/link";
import { useParams, useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";

import { ApiError, problemTitle } from "@/lib/api/client";
import {
  useDeleteDocument,
  useDeletePurchase,
  usePurchase,
  useUpdatePurchase,
  useUploadPurchaseProof,
} from "@/lib/api/hooks";
import type { PurchaseDetailResponse } from "@/lib/api/types";
import { formatDate } from "@/lib/format";
import { documentCategoryLabels, problemTypeLabels, statusLabels } from "@/lib/labels";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { DateWithSource } from "@/components/date-with-source";
import { EmptyState } from "@/components/empty-state";
import { FileDropZone } from "@/components/file-drop-zone";
import { InlineError } from "@/components/inline-error";
import { Money } from "@/components/money";
import {
  PurchaseForm,
  validatePurchaseForm,
  type PurchaseFormValues,
} from "@/components/purchase-form";
import { StatusBadge } from "@/components/status-badge";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Skeleton } from "@/components/ui/skeleton";

function toFormValues(p: PurchaseDetailResponse): PurchaseFormValues {
  return {
    merchantName: p.merchantName,
    orderNumber: p.orderNumber ?? "",
    purchaseDate: p.purchaseDate ?? "",
    currency: p.currency,
    totalAmount: p.totalAmount?.toString() ?? "",
    notes: p.notes ?? "",
    items:
      p.items.length > 0
        ? p.items.map((i) => ({
            productName: i.productName,
            quantity: String(i.quantity),
            unitPrice: i.unitPrice?.toString() ?? "",
          }))
        : [{ productName: "", quantity: "1", unitPrice: "" }],
  };
}

export default function PurchaseDetailPage() {
  const params = useParams<{ id: string }>();
  const router = useRouter();
  const id = params.id;
  const { data: purchase, isLoading, error, refetch } = usePurchase(id);
  const update = useUpdatePurchase(id);
  const del = useDeletePurchase(id);
  const upload = useUploadPurchaseProof(id);
  const deleteDoc = useDeleteDocument();

  const [editing, setEditing] = useState(false);
  const [formValues, setFormValues] = useState<PurchaseFormValues | null>(null);
  const [formErrors, setFormErrors] = useState<Record<string, string>>({});
  const [saveError, setSaveError] = useState<unknown>(null);
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [deleteError, setDeleteError] = useState<string | null>(null);
  const [progress, setProgress] = useState<number | null>(null);
  const [docConfirm, setDocConfirm] = useState<string | null>(null);

  if (isLoading) {
    return (
      <div className="space-y-4" aria-busy="true">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-48 rounded-2xl" />
      </div>
    );
  }
  if (error || !purchase) {
    if (error instanceof ApiError && error.status === 404) {
      return (
        <EmptyState
          icon={SearchXIcon}
          title="This purchase doesn't exist or was deleted."
          description="It may have been removed."
        >
          <Button onClick={() => router.push("/app/purchases")}>Back to purchases</Button>
        </EmptyState>
      );
    }
    return <InlineError error={error} onRetry={() => refetch()} />;
  }

  const save = async () => {
    if (!formValues) return;
    const errs = validatePurchaseForm(formValues);
    setFormErrors(errs);
    if (Object.keys(errs).length > 0) return;
    setSaveError(null);
    try {
      await update.mutateAsync({
        merchantName: formValues.merchantName.trim(),
        orderNumber: formValues.orderNumber.trim() || null,
        purchaseDate: formValues.purchaseDate || null,
        currency: formValues.currency,
        totalAmount:
          formValues.totalAmount.trim() === "" ? null : Number(formValues.totalAmount),
        notes: formValues.notes.trim() || null,
        items: formValues.items.map((item, i) => ({
          id: purchase.items[i]?.id ?? null,
          productName: item.productName.trim(),
          quantity: Number(item.quantity) || 1,
          unitPrice: item.unitPrice.trim() === "" ? null : Number(item.unitPrice),
        })),
      });
      toast.success("Purchase updated.");
      setEditing(false);
    } catch (e) {
      setSaveError(e);
    }
  };

  return (
    <div className="space-y-5">
      <nav aria-label="Breadcrumb">
        <Link
          href="/app/purchases"
          className="inline-flex min-h-11 items-center gap-1 text-sm text-muted-foreground hover:text-foreground"
        >
          <ArrowLeftIcon className="size-4" aria-hidden /> Purchases
        </Link>
      </nav>

      <header className="flex flex-wrap items-start justify-between gap-3">
        <div className="min-w-0">
          <h1 className="text-2xl font-semibold tracking-tight">{purchase.merchantName}</h1>
          {purchase.orderNumber ? (
            <p className="mt-0.5 font-mono text-sm text-muted-foreground">
              {purchase.orderNumber}
            </p>
          ) : null}
        </div>
        <div className="flex gap-2">
          <Button
            variant="outline"
            className="touch-target"
            onClick={() => {
              setFormValues(toFormValues(purchase));
              setEditing((v) => !v);
            }}
          >
            <PencilIcon aria-hidden /> {editing ? "Cancel" : "Edit"}
          </Button>
          <Button
            className="touch-target"
            onClick={() => router.push(`/app/cases/new?purchaseId=${id}`)}
          >
            <PlusIcon aria-hidden /> Start a case about this purchase
          </Button>
        </div>
      </header>

      {deleteError ? (
        <p role="alert" className="rounded-xl bg-destructive/10 px-4 py-3 text-sm text-destructive">
          {deleteError}
        </p>
      ) : null}

      {editing && formValues ? (
        <Card>
          <CardContent className="space-y-4 pt-5">
            <PurchaseForm
              values={formValues}
              onChange={setFormValues}
              errors={formErrors}
              idPrefix="edit"
            />
            {saveError ? <InlineError error={saveError} onRetry={save} /> : null}
            <Button className="touch-target" onClick={save} disabled={update.isPending}>
              {update.isPending ? (
                <>
                  <Loader2Icon className="animate-spin" aria-hidden /> Saving…
                </>
              ) : (
                "Save changes"
              )}
            </Button>
          </CardContent>
        </Card>
      ) : (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Details</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4 text-sm">
            <dl className="grid gap-x-6 gap-y-2 sm:grid-cols-2">
              <div>
                <dt className="text-xs text-muted-foreground">Purchase date</dt>
                <dd className="mt-0.5">
                  {purchase.purchaseDate ? (
                    <DateWithSource
                      date={purchase.purchaseDate}
                      provenance={purchase.provenance.purchaseDate}
                    />
                  ) : (
                    "—"
                  )}
                </dd>
              </div>
              <div>
                <dt className="text-xs text-muted-foreground">Total</dt>
                <dd className="mt-0.5">
                  <Money amount={purchase.totalAmount} currency={purchase.currency} />
                </dd>
              </div>
            </dl>
            {purchase.items.length > 0 ? (
              <div>
                <p className="text-xs font-medium text-muted-foreground">Items</p>
                <ul className="mt-1.5 space-y-2">
                  {purchase.items.map((item) => (
                    <li
                      key={item.id}
                      className="flex flex-wrap items-baseline justify-between gap-x-4 gap-y-1 rounded-lg border border-border px-3 py-2"
                    >
                      <span>
                        {item.productName}
                        {item.quantity > 1 ? (
                          <span className="text-muted-foreground"> ×{item.quantity}</span>
                        ) : null}
                      </span>
                      <span className="flex flex-wrap items-baseline gap-3 text-xs text-muted-foreground">
                        {item.unitPrice !== null ? (
                          <Money amount={item.unitPrice} currency={purchase.currency} />
                        ) : null}
                        {item.returnDeadline ? (
                          <DateWithSource
                            date={item.returnDeadline}
                            provenance={item.returnDeadlineProvenance}
                          />
                        ) : null}
                        {item.commercialWarrantyEnd ? (
                          <DateWithSource
                            date={item.commercialWarrantyEnd}
                            provenance={item.commercialWarrantyEndProvenance}
                          />
                        ) : null}
                      </span>
                    </li>
                  ))}
                </ul>
              </div>
            ) : null}
          </CardContent>
        </Card>
      )}

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Documents</CardTitle>
        </CardHeader>
        <CardContent className="space-y-3">
          {purchase.documents.length === 0 ? (
            <p className="text-sm text-muted-foreground">No documents yet.</p>
          ) : (
            <ul className="space-y-2">
              {purchase.documents.map((doc) => (
                <li
                  key={doc.id}
                  className="flex items-center gap-3 rounded-xl border border-border p-3"
                >
                  <FileTextIcon className="size-4 shrink-0 text-muted-foreground" aria-hidden />
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium">{doc.fileName}</p>
                    <p className="text-xs text-muted-foreground">
                      {documentCategoryLabels[doc.category] ?? doc.category} ·{" "}
                      {formatDate(doc.createdAt)}
                    </p>
                  </div>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="size-9"
                    render={
                      <a
                        href={`/api/documents/${doc.id}?download=1`}
                        download={doc.fileName}
                        aria-label={`Download ${doc.fileName}`}
                      />
                    }
                  >
                    <DownloadIcon aria-hidden />
                  </Button>
                  <Button
                    variant="ghost"
                    size="icon"
                    className="size-9 text-destructive"
                    aria-label={`Remove ${doc.fileName}`}
                    onClick={() => setDocConfirm(doc.id)}
                  >
                    <Trash2Icon aria-hidden />
                  </Button>
                </li>
              ))}
            </ul>
          )}
          <FileDropZone
            label="Add proof (receipt, confirmation…)"
            progress={progress}
            onFile={async (file) => {
              try {
                await upload.mutateAsync({ file, onProgress: setProgress });
                toast.success("Document added.");
              } catch (e) {
                toast.error(problemTitle(e));
              } finally {
                setProgress(null);
              }
            }}
          />
        </CardContent>
      </Card>

      <Card>
        <CardHeader className="pb-2">
          <CardTitle className="text-base">Linked cases</CardTitle>
        </CardHeader>
        <CardContent>
          {purchase.cases.length === 0 ? (
            <p className="text-sm text-muted-foreground">
              No cases about this purchase yet.
            </p>
          ) : (
            <ul className="space-y-2">
              {purchase.cases.map((c) => (
                <li key={c.id}>
                  <Link
                    href={`/app/cases/${c.id}`}
                    className="flex min-h-11 items-center justify-between gap-3 rounded-xl border border-border px-3 py-2 hover:bg-muted/50"
                  >
                    <span className="text-sm">
                      {problemTypeLabels[c.problemType]} · {statusLabels[c.status]}
                    </span>
                    <StatusBadge status={c.status} />
                  </Link>
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>

      <div className="pt-2">
        <Button
          variant="ghost"
          className="touch-target text-destructive"
          onClick={() => setDeleteOpen(true)}
        >
          <Trash2Icon aria-hidden /> Delete purchase
        </Button>
      </div>

      <ConfirmDialog
        open={deleteOpen}
        onOpenChange={setDeleteOpen}
        title="Delete this purchase?"
        description="The purchase, its items and uploaded documents will be permanently deleted."
        confirmLabel="Delete purchase"
        onConfirm={async () => {
          try {
            await del.mutateAsync();
            toast.success("Purchase deleted.");
            router.push("/app/purchases");
          } catch (e) {
            if (e instanceof ApiError && e.status === 409) {
              setDeleteError(problemTitle(e));
            } else {
              setDeleteError(problemTitle(e));
            }
          }
        }}
      />
      <ConfirmDialog
        open={docConfirm !== null}
        onOpenChange={(o) => !o && setDocConfirm(null)}
        title="Remove this document?"
        description="The file will be permanently deleted."
        confirmLabel="Remove"
        onConfirm={async () => {
          if (docConfirm) {
            await deleteDoc.mutateAsync(docConfirm);
            toast.success("Document removed.");
          }
        }}
      />
    </div>
  );
}
