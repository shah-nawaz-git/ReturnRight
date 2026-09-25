import {
  CircleAlertIcon,
  CircleHelpIcon,
  PackageIcon,
  PackageXIcon,
  ReceiptIcon,
  TruckIcon,
  WrenchIcon,
  type LucideIcon,
} from "lucide-react";

import type { ProblemType } from "@/lib/api/types";
import { cn } from "@/lib/utils";

const icons: Record<ProblemType, LucideIcon> = {
  DamagedItem: PackageXIcon,
  DefectiveItem: WrenchIcon,
  WrongItemReceived: PackageIcon,
  MissingItem: CircleHelpIcon,
  DeliveryProblem: TruckIcon,
  RefundProblem: ReceiptIcon,
  Other: CircleAlertIcon,
};

export function ProblemTypeIcon({
  type,
  className,
}: {
  type: ProblemType;
  className?: string;
}) {
  const Icon = icons[type] ?? CircleAlertIcon;
  return <Icon className={cn("size-4", className)} aria-hidden />;
}
