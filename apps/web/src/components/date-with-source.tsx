import { formatDate } from "@/lib/format";
import type { ProvenanceResponse } from "@/lib/api/types";
import { cn } from "@/lib/utils";

function sourceText(p: ProvenanceResponse | null | undefined): string | null {
  if (!p) return null;
  if (p.source === "UserEntered") return "Entered by you";
  if (p.source === "ExtractedFromDocument") {
    return p.confirmedByUser
      ? "From your invoice · confirmed"
      : "From your invoice · check this";
  }
  return null;
}

export function DateWithSource({
  date,
  provenance,
  className,
}: {
  date: string | null | undefined;
  provenance?: ProvenanceResponse | null;
  className?: string;
}) {
  if (!date) return <span className="text-muted-foreground">—</span>;
  const source = sourceText(provenance);
  const unconfirmed =
    provenance?.source === "ExtractedFromDocument" && !provenance.confirmedByUser;
  return (
    <span className={cn("inline-flex flex-wrap items-baseline gap-x-1.5", className)}>
      <span>{formatDate(date)}</span>
      {source ? (
        <span
          className={cn(
            "text-xs",
            unconfirmed ? "text-warning" : "text-muted-foreground",
          )}
        >
          {source}
        </span>
      ) : null}
    </span>
  );
}
