import { AlertCircleIcon, FileTextIcon } from "lucide-react";
import { cloneElement, isValidElement, type ReactElement, type ReactNode } from "react";

import { cn } from "@/lib/utils";
import { Label } from "@/components/ui/label";

export function FormField({
  label,
  htmlFor,
  error,
  hint,
  required,
  sourceTag,
  children,
}: {
  label: string;
  htmlFor?: string;
  error?: string;
  hint?: string;
  required?: boolean;
  /** "document" = extracted ≥0.75; "check" = extracted <0.75 */
  sourceTag?: "document" | "check" | null;
  children: ReactNode;
}) {
  const describedBy =
    [error && `${htmlFor}-error`, hint && `${htmlFor}-hint`]
      .filter(Boolean)
      .join(" ") || undefined;

  // Inject aria attributes into a single child input when possible.
  const content =
    isValidElement(children) && htmlFor
      ? cloneElement(children as ReactElement<Record<string, unknown>>, {
          "aria-invalid": error ? true : undefined,
          "aria-describedby": describedBy,
        })
      : children;

  return (
    <div className={cn("space-y-1.5", error && "has-error")}>
      <div className="flex items-center justify-between gap-2">
        <Label htmlFor={htmlFor}>
          {label}
          {required ? (
            <span className="text-destructive" aria-hidden>
              {" "}
              *
            </span>
          ) : null}
        </Label>
        {sourceTag === "document" ? (
          <span className="inline-flex items-center gap-1 text-xs text-muted-foreground">
            <FileTextIcon className="size-3" aria-hidden />
            From your document
          </span>
        ) : null}
        {sourceTag === "check" ? (
          <span className="inline-flex items-center gap-1 text-xs text-warning">
            <AlertCircleIcon className="size-3" aria-hidden />
            Check this
          </span>
        ) : null}
      </div>
      {content}
      {hint ? (
        <p
          id={htmlFor ? `${htmlFor}-hint` : undefined}
          className="text-xs text-muted-foreground"
        >
          {hint}
        </p>
      ) : null}
      {error ? (
        <p
          id={htmlFor ? `${htmlFor}-error` : undefined}
          role="alert"
          className="text-xs text-destructive"
        >
          {error}
        </p>
      ) : null}
    </div>
  );
}
