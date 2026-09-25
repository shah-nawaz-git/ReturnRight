"use client";

import { CircleAlertIcon, Loader2Icon } from "lucide-react";
import { useEffect, useState } from "react";

import { useCreateIntake, useDeleteIntake, useIntake } from "@/lib/api/hooks";
import { problemTitle } from "@/lib/api/client";
import type { IntakeResponse } from "@/lib/api/types";
import { FileDropZone } from "@/components/file-drop-zone";
import { Button } from "@/components/ui/button";

export function IntakeUploader({
  onResolved,
  onFileChanged,
}: {
  /** Called when extraction finishes — success or failure. */
  onResolved: (intake: IntakeResponse) => void;
  /** File selected but extraction still running. */
  onFileChanged?: (file: { name: string; size: number } | null) => void;
}) {
  const createIntake = useCreateIntake();
  const deleteIntake = useDeleteIntake();
  const [intakeId, setIntakeId] = useState<string | null>(null);
  const [fileMeta, setFileMeta] = useState<{ name: string; size: number } | null>(null);
  const [progress, setProgress] = useState<number | null>(null);
  const [error, setError] = useState<string | null>(null);
  const { data: intake } = useIntake(intakeId);

  useEffect(() => {
    if (intake && intake.status !== "processing") {
      onResolved(intake);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [intake?.status]);

  const handleFile = async (file: File) => {
    setError(null);
    setFileMeta({ name: file.name, size: file.size });
    onFileChanged?.({ name: file.name, size: file.size });
    setProgress(0);
    try {
      const result = await createIntake.mutateAsync({
        file,
        onProgress: setProgress,
      });
      setProgress(null);
      setIntakeId(result.id);
      if (result.status !== "processing") {
        onResolved(result);
      }
    } catch (e) {
      setProgress(null);
      setFileMeta(null);
      onFileChanged?.(null);
      setError(problemTitle(e, "The upload didn't work. Try again."));
    }
  };

  const reset = async () => {
    if (intakeId) {
      await deleteIntake.mutateAsync(intakeId).catch(() => undefined);
    }
    setIntakeId(null);
    setFileMeta(null);
    setProgress(null);
    onFileChanged?.(null);
  };

  const processing = intakeId !== null && intake?.status !== "failed" && intake?.status !== "succeeded";

  return (
    <div className="space-y-3">
      <FileDropZone
        onFile={handleFile}
        progress={progress}
        file={fileMeta}
        disabled={processing}
      />
      {processing ? (
        <div className="flex items-center gap-2 rounded-xl border border-border bg-card px-4 py-3 text-sm text-muted-foreground" role="status">
          <Loader2Icon className="size-4 animate-spin" aria-hidden />
          Reading your document…
        </div>
      ) : null}
      {intake?.status === "failed" ? (
        <div className="flex items-start gap-2 rounded-xl border border-warning/40 bg-warning/5 px-4 py-3 text-sm" role="status">
          <CircleAlertIcon className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden />
          <span>
            We couldn&apos;t read this document automatically. You can still enter
            the purchase details manually.
          </span>
        </div>
      ) : null}
      {fileMeta && !processing ? (
        <Button variant="ghost" size="sm" onClick={reset} className="touch-target">
          Use a different file
        </Button>
      ) : null}
      {error ? (
        <p role="alert" className="text-sm text-destructive">
          {error}
        </p>
      ) : null}
    </div>
  );
}
