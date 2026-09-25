"use client";

import { BellIcon, CheckCheckIcon, Trash2Icon } from "lucide-react";
import { useRouter } from "next/navigation";
import { toast } from "sonner";

import {
  useDeleteNotification,
  useMarkAllNotificationsRead,
  useMarkNotificationRead,
  useNotifications,
} from "@/lib/api/hooks";
import type { NotificationResponse } from "@/lib/api/types";
import { formatRelativeUpdated } from "@/lib/format";
import { cn } from "@/lib/utils";
import { EmptyState } from "@/components/empty-state";
import { InlineError } from "@/components/inline-error";
import { PageHeader } from "@/components/page-header";
import { Button } from "@/components/ui/button";
import { Skeleton } from "@/components/ui/skeleton";

export default function NotificationsPage() {
  const router = useRouter();
  const { data, isLoading, error, refetch } = useNotifications();
  const markRead = useMarkNotificationRead();
  const markAll = useMarkAllNotificationsRead();
  const del = useDeleteNotification();

  const hasUnread = (data ?? []).some((n) => !n.readAt);

  const open = async (n: NotificationResponse) => {
    if (!n.readAt) {
      await markRead.mutateAsync(n.id).catch(() => undefined);
    }
    if (n.caseId) router.push(`/app/cases/${n.caseId}`);
  };

  return (
    <div className="space-y-5">
      <PageHeader
        title="Notifications"
        actions={
          hasUnread ? (
            <Button
              variant="outline"
              className="touch-target"
              onClick={async () => {
                await markAll.mutateAsync();
                toast.success("All marked as read.");
              }}
              disabled={markAll.isPending}
            >
              <CheckCheckIcon aria-hidden /> Mark all as read
            </Button>
          ) : undefined
        }
      />

      {isLoading ? (
        <ul className="space-y-2" aria-busy="true">
          {[0, 1, 2].map((i) => (
            <li key={i}>
              <Skeleton className="h-20 rounded-2xl" />
            </li>
          ))}
        </ul>
      ) : error ? (
        <InlineError error={error} onRetry={() => refetch()} />
      ) : !data || data.length === 0 ? (
        <EmptyState
          icon={BellIcon}
          title="Nothing here yet"
          description="We'll let you know when a follow-up is due or something needs your attention."
        />
      ) : (
        <ul className="space-y-2">
          {data.map((n) => (
            <li
              key={n.id}
              className={cn(
                "flex items-start gap-3 rounded-2xl border border-border p-4",
                n.readAt ? "bg-card" : "bg-primary/5 border-primary/20",
              )}
            >
              <button
                type="button"
                onClick={() => open(n)}
                className="min-w-0 flex-1 rounded-lg text-left"
              >
                <span className="flex items-center gap-2">
                  {!n.readAt ? (
                    <span className="size-2 shrink-0 rounded-full bg-primary" aria-hidden />
                  ) : null}
                  <span className={cn("truncate text-sm", !n.readAt && "font-medium")}>
                    {n.title}
                  </span>
                </span>
                <span className="mt-0.5 block text-sm text-muted-foreground">{n.body}</span>
                <span className="mt-1 block text-xs text-muted-foreground">
                  {formatRelativeUpdated(n.createdAt)}
                </span>
              </button>
              <Button
                variant="ghost"
                size="icon"
                className="size-9 shrink-0 text-muted-foreground"
                aria-label="Delete notification"
                onClick={() => del.mutate(n.id)}
              >
                <Trash2Icon aria-hidden />
              </Button>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
