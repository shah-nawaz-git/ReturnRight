"use client";

import { CircleAlertIcon, RotateCwIcon } from "lucide-react";

import { problemTitle } from "@/lib/api/client";
import { Button } from "@/components/ui/button";

export function InlineError({
  error,
  onRetry,
  title = "Something went wrong",
}: {
  error: unknown;
  onRetry?: () => void;
  title?: string;
}) {
  return (
    <div
      role="alert"
      className="flex items-start gap-3 rounded-xl border border-destructive/30 bg-destructive/5 px-4 py-3"
    >
      <CircleAlertIcon className="mt-0.5 size-4 shrink-0 text-destructive" aria-hidden />
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium text-destructive">{title}</p>
        <p className="mt-0.5 text-sm text-muted-foreground">{problemTitle(error)}</p>
      </div>
      {onRetry ? (
        <Button
          variant="outline"
          size="sm"
          className="touch-target shrink-0"
          onClick={onRetry}
        >
          <RotateCwIcon aria-hidden />
          Retry
        </Button>
      ) : null}
    </div>
  );
}
