"use client";

import { useRouter } from "next/navigation";
import { useState } from "react";
import { toast } from "sonner";

import { problemTitle } from "@/lib/api/client";
import {
  useDeleteAccount,
  useLogout,
  useMe,
  useUpdateProfile,
} from "@/lib/api/hooks";
import { ConfirmDialog } from "@/components/confirm-dialog";
import { PageHeader } from "@/components/page-header";
import { PasswordInput } from "@/components/auth/password-input";
import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Label } from "@/components/ui/label";
import { Skeleton } from "@/components/ui/skeleton";
import { Switch } from "@/components/ui/switch";

export default function ProfilePage() {
  const router = useRouter();
  const { data: me, isLoading } = useMe();
  const update = useUpdateProfile();
  const logout = useLogout();
  const deleteAccount = useDeleteAccount();

  const [deleteOpen, setDeleteOpen] = useState(false);
  const [password, setPassword] = useState("");
  const [deleteError, setDeleteError] = useState<string | null>(null);

  const toggle = async (key: "emailRemindersEnabled" | "inAppRemindersEnabled", value: boolean) => {
    try {
      await update.mutateAsync({ [key]: value });
      toast.success("Saved.");
    } catch (e) {
      toast.error(problemTitle(e));
    }
  };

  return (
    <div className="space-y-5">
      <PageHeader title="Profile" />

      {isLoading || !me ? (
        <Skeleton className="h-40 rounded-2xl" aria-busy="true" />
      ) : (
        <Card>
          <CardHeader className="pb-2">
            <CardTitle className="text-base">Account</CardTitle>
          </CardHeader>
          <CardContent className="space-y-4">
            <div>
              <p className="text-xs text-muted-foreground">Email</p>
              <p className="text-sm font-medium">{me.email}</p>
            </div>
            <div className="space-y-1">
              <div className="flex min-h-11 items-center justify-between gap-4">
                <Label htmlFor="pref-email" className="text-sm font-normal">
                  Email reminders
                  <span className="block text-xs text-muted-foreground">
                    We&apos;ll email you when a follow-up is due.
                  </span>
                </Label>
                <Switch
                  id="pref-email"
                  checked={me.emailRemindersEnabled}
                  onCheckedChange={(v) => toggle("emailRemindersEnabled", v)}
                  disabled={update.isPending}
                />
              </div>
              <div className="flex min-h-11 items-center justify-between gap-4">
                <Label htmlFor="pref-app" className="text-sm font-normal">
                  In-app reminders
                  <span className="block text-xs text-muted-foreground">
                    Show reminders in the notifications list.
                  </span>
                </Label>
                <Switch
                  id="pref-app"
                  checked={me.inAppRemindersEnabled}
                  onCheckedChange={(v) => toggle("inAppRemindersEnabled", v)}
                  disabled={update.isPending}
                />
              </div>
            </div>
            <Button
              variant="outline"
              className="touch-target"
              onClick={async () => {
                await logout.mutateAsync();
                router.push("/");
              }}
              disabled={logout.isPending}
            >
              Log out
            </Button>
          </CardContent>
        </Card>
      )}

      <Card className="border-destructive/30">
        <CardHeader className="pb-2">
          <CardTitle className="text-base text-destructive">Danger zone</CardTitle>
        </CardHeader>
        <CardContent>
          <p className="text-sm text-muted-foreground">
            Deleting your account permanently removes your cases, purchases and
            uploaded files. This can&apos;t be undone.
          </p>
          <Button
            variant="destructive"
            className="touch-target mt-3"
            onClick={() => setDeleteOpen(true)}
          >
            Delete account
          </Button>
        </CardContent>
      </Card>

      {deleteOpen ? (
        <ConfirmDialog
          open={deleteOpen}
          onOpenChange={(o) => {
            setDeleteOpen(o);
            if (!o) {
              setPassword("");
              setDeleteError(null);
            }
          }}
          title="Delete your account?"
          description="Your cases, purchases and uploaded files will be permanently deleted."
          confirmLabel="Delete my account"
          confirmDisabled={password.length === 0 || deleteAccount.isPending}
          onConfirm={async () => {
            try {
              await deleteAccount.mutateAsync({ password });
              toast.success("Account deleted.");
              router.push("/");
            } catch (e) {
              setDeleteError(problemTitle(e));
              throw e;
            }
          }}
        >
          <div className="space-y-2">
            <Label htmlFor="del-password">Confirm your password</Label>
            <PasswordInput
              id="del-password"
              value={password}
              onChange={(e) => setPassword(e.target.value)}
              autoComplete="current-password"
            />
            {deleteError ? (
              <p role="alert" className="text-xs text-destructive">
                {deleteError}
              </p>
            ) : null}
          </div>
        </ConfirmDialog>
      ) : null}
    </div>
  );
}
