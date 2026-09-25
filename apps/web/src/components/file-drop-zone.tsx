"use client";

import { CameraIcon, FileIcon, UploadCloudIcon, XIcon } from "lucide-react";
import { useCallback, useRef, useState } from "react";
import { Button as RacButton, DropZone, FileTrigger } from "react-aria-components";

import { formatBytes } from "@/lib/format";
import { cn } from "@/lib/utils";
import { Button, buttonVariants } from "@/components/ui/button";
import { Progress, ProgressIndicator, ProgressTrack } from "@/components/ui/progress";

const MAX_BYTES = 10 * 1024 * 1024;
const ACCEPTED_TYPES = ["application/pdf", "image/jpeg", "image/png"];
const ACCEPTED_EXTENSIONS = [".pdf", ".jpg", ".jpeg", ".png"];

// Same messages the API's UploadValidator returns.
export function validateUpload(file: File): string | null {
  if (file.size <= 0) return "This file appears to be empty.";
  if (file.size > MAX_BYTES) return "This file is larger than 10 MB.";
  const ext = file.name.slice(file.name.lastIndexOf(".")).toLowerCase();
  const typeOk =
    file.type === "" ||
    file.type === "application/octet-stream" ||
    ACCEPTED_TYPES.includes(file.type);
  if (!typeOk || !ACCEPTED_EXTENSIONS.includes(ext)) {
    return "We can only accept PDF, JPG and PNG files.";
  }
  return null;
}

export interface FileDropZoneProps {
  onFile: (file: File) => void;
  progress?: number | null;
  file?: { name: string; size: number } | null;
  onClear?: () => void;
  disabled?: boolean;
  label?: string;
  className?: string;
}

export function FileDropZone({
  onFile,
  progress = null,
  file = null,
  onClear,
  disabled = false,
  label = "Drag a file here, or",
  className,
}: FileDropZoneProps) {
  const [error, setError] = useState<string | null>(null);
  const [dragging, setDragging] = useState(false);
  const statusRef = useRef<HTMLDivElement>(null);

  const accept = useCallback(
    (f: File | null | undefined) => {
      if (!f) return;
      const problem = validateUpload(f);
      if (problem) {
        setError(problem);
        return;
      }
      setError(null);
      onFile(f);
    },
    [onFile],
  );

  if (file) {
    return (
      <div className={className}>
        <div className="flex items-center gap-3 rounded-xl border border-border bg-card px-4 py-3">
          <FileIcon className="size-5 shrink-0 text-muted-foreground" aria-hidden />
          <div className="min-w-0 flex-1">
            <p className="truncate text-sm font-medium">{file.name}</p>
            <p className="text-xs text-muted-foreground">{formatBytes(file.size)}</p>
          </div>
          {progress !== null && progress < 1 ? (
            <div className="w-24">
              <Progress value={progress * 100} aria-label="Upload progress">
                <ProgressTrack>
                  <ProgressIndicator />
                </ProgressTrack>
              </Progress>
            </div>
          ) : null}
          {onClear && progress === null ? (
            <Button
              variant="ghost"
              size="icon"
              className="touch-target"
              aria-label="Remove file"
              onClick={onClear}
            >
              <XIcon aria-hidden />
            </Button>
          ) : null}
        </div>
        <div ref={statusRef} role="status" className="sr-only">
          {progress !== null ? `Uploading ${Math.round(progress * 100)}%` : ""}
        </div>
      </div>
    );
  }

  return (
    <div className={className}>
      <DropZone
        aria-label="Upload a file"
        isDisabled={disabled}
        className={cn(
          "flex flex-col items-center justify-center gap-3 rounded-xl border-2 border-dashed px-6 py-10 text-center transition-colors",
          dragging ? "border-primary bg-primary/5" : "border-border bg-card",
          disabled && "opacity-50",
        )}
        onDrop={async (e) => {
          setDragging(false);
          const item = e.items.find((i) => i.kind === "file");
          if (item && item.kind === "file") {
            accept(await item.getFile());
          }
        }}
        onDropEnter={() => setDragging(true)}
        onDropExit={() => setDragging(false)}
      >
        <UploadCloudIcon className="size-8 text-muted-foreground" aria-hidden />
        <p className="text-sm text-muted-foreground">{label}</p>
        <div className="flex flex-wrap items-center justify-center gap-2">
          <FileTrigger
            acceptedFileTypes={ACCEPTED_TYPES}
            onSelect={(files) => accept(files?.[0])}
          >
            {/* RAC FileTrigger injects press behavior — must be a RAC Button,
                not the base-ui one. */}
            <RacButton
              isDisabled={disabled}
              className={buttonVariants({ variant: "outline", className: "touch-target" })}
            >
              Choose file
            </RacButton>
          </FileTrigger>
          <FileTrigger
            acceptedFileTypes={["image/jpeg", "image/png"]}
            defaultCamera="environment"
            onSelect={(files) => accept(files?.[0])}
          >
            <RacButton
              isDisabled={disabled}
              className={buttonVariants({ variant: "outline", className: "touch-target sm:hidden" })}
            >
              <CameraIcon aria-hidden />
              Take a photo
            </RacButton>
          </FileTrigger>
        </div>
        <p className="text-xs text-muted-foreground">PDF, JPG or PNG · up to 10 MB</p>
      </DropZone>
      {error ? (
        <p role="alert" className="mt-2 text-sm text-destructive">
          {error}
        </p>
      ) : null}
      <div role="status" className="sr-only" aria-live="polite">
        {progress !== null ? `Uploading ${Math.round(progress * 100)}%` : ""}
      </div>
    </div>
  );
}
