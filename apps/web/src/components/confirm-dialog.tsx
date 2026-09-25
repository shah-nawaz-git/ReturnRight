"use client";

import { useState, type ReactNode } from "react";

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from "@/components/ui/alert-dialog";

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  consequences,
  confirmLabel = "Delete",
  confirmDisabled = false,
  onConfirm,
  busy = false,
  children,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description?: string;
  consequences?: string[];
  confirmLabel?: string;
  confirmDisabled?: boolean;
  onConfirm: () => void | Promise<void>;
  busy?: boolean;
  children?: ReactNode;
}) {
  const [pending, setPending] = useState(false);
  const working = busy || pending;

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          {description ? (
            <AlertDialogDescription>{description}</AlertDialogDescription>
          ) : null}
        </AlertDialogHeader>
        {consequences && consequences.length > 0 ? (
          <ul className="list-disc space-y-1 pl-5 text-sm text-muted-foreground">
            {consequences.map((c) => (
              <li key={c}>{c}</li>
            ))}
          </ul>
        ) : null}
        {children}
        <AlertDialogFooter>
          <AlertDialogCancel className="touch-target">Cancel</AlertDialogCancel>
          <AlertDialogAction
            variant="destructive"
            className="touch-target"
            disabled={confirmDisabled || working}
            onClick={async (e) => {
              e.preventDefault();
              setPending(true);
              try {
                await onConfirm();
                onOpenChange(false);
              } catch {
                // Callers surface errors themselves; keep the dialog open.
              } finally {
                setPending(false);
              }
            }}
          >
            {working ? "Working…" : confirmLabel}
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
