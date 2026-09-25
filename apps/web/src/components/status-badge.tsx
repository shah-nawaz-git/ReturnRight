import {
  CheckCircle2Icon,
  CircleIcon,
  ClockIcon,
  MailIcon,
  PackageIcon,
  RefreshCwIcon,
  TruckIcon,
  XCircleIcon,
  type LucideIcon,
} from "lucide-react";

import { statusLabels } from "@/lib/labels";
import type { CaseStatus } from "@/lib/api/types";
import { cn } from "@/lib/utils";

const config: Record<CaseStatus, { icon: LucideIcon; className: string }> = {
  Open: { icon: CircleIcon, className: "bg-muted text-muted-foreground" },
  SellerContacted: { icon: MailIcon, className: "bg-primary/10 text-primary" },
  WaitingForSeller: { icon: ClockIcon, className: "bg-primary/10 text-primary" },
  ReturnInProgress: { icon: TruckIcon, className: "bg-warning/10 text-warning" },
  RefundPending: { icon: RefreshCwIcon, className: "bg-warning/10 text-warning" },
  ReplacementPending: { icon: PackageIcon, className: "bg-warning/10 text-warning" },
  Resolved: { icon: CheckCircle2Icon, className: "bg-success/10 text-success" },
  Closed: { icon: XCircleIcon, className: "bg-muted text-muted-foreground" },
};

export function StatusBadge({ status, className }: { status: CaseStatus; className?: string }) {
  const { icon: Icon, className: colors } = config[status] ?? config.Open;
  return (
    <span
      className={cn(
        "inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-medium",
        colors,
        className,
      )}
    >
      <Icon className="size-3.5" aria-hidden />
      {statusLabels[status] ?? status}
    </span>
  );
}
