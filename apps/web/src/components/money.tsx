import { formatMoney } from "@/lib/format";
import { cn } from "@/lib/utils";

export function Money({
  amount,
  currency,
  className,
}: {
  amount: number | null | undefined;
  currency?: string | null;
  className?: string;
}) {
  return (
    <span className={cn("tabular-nums", className)}>
      {formatMoney(amount, currency)}
    </span>
  );
}
