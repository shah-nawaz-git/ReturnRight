"use client";

import {
  BellIcon,
  BriefcaseIcon,
  HouseIcon,
  PackageCheckIcon,
  PlusIcon,
  UserIcon,
} from "lucide-react";
import Link from "next/link";
import { usePathname } from "next/navigation";
import type { ReactNode } from "react";

import { useMe } from "@/lib/api/hooks";
import { cn } from "@/lib/utils";

const desktopNav = [
  { href: "/app", label: "Home", exact: true },
  { href: "/app/cases", label: "My cases", exact: false },
  { href: "/app/purchases", label: "Purchases", exact: false },
  { href: "/app/notifications", label: "Notifications", exact: false, badge: true },
  { href: "/app/profile", label: "Profile", exact: false },
];

const mobileNav = [
  { href: "/app", label: "Home", icon: HouseIcon, exact: true },
  { href: "/app/cases", label: "Cases", icon: BriefcaseIcon, exact: false },
  { href: "__cta__", label: "Start a case", icon: PlusIcon, exact: false },
  { href: "/app/notifications", label: "Alerts", icon: BellIcon, exact: false, badge: true },
  { href: "/app/profile", label: "Profile", icon: UserIcon, exact: false },
];

function isActive(pathname: string, href: string, exact: boolean) {
  return exact ? pathname === href : pathname === href || pathname.startsWith(href + "/");
}

export function Logo({ className }: { className?: string }) {
  return (
    <span className={cn("inline-flex items-center gap-2", className)}>
      <PackageCheckIcon className="size-5 text-primary" aria-hidden />
      <span className="text-base font-semibold tracking-tight">ReturnRight</span>
    </span>
  );
}

export function AppShell({ children }: { children: ReactNode }) {
  const pathname = usePathname();
  const { data: me } = useMe();
  const unread = me?.unreadNotifications ?? 0;

  return (
    <div className="flex min-h-dvh flex-col">
      <header className="sticky top-0 z-40 border-b border-border bg-card/95 backdrop-blur supports-[backdrop-filter]:bg-card/80">
        <div className="mx-auto flex h-14 w-full max-w-5xl items-center justify-between px-4">
          <Link href="/app" aria-label="ReturnRight home">
            <Logo />
          </Link>
          <nav aria-label="Main" className="hidden items-center gap-1 sm:flex">
            {desktopNav.map((item) => (
              <Link
                key={item.href}
                href={item.href}
                aria-current={isActive(pathname, item.href, item.exact) ? "page" : undefined}
                className={cn(
                  "relative rounded-lg px-3 py-2 text-sm font-medium transition-colors",
                  isActive(pathname, item.href, item.exact)
                    ? "bg-muted text-foreground"
                    : "text-muted-foreground hover:text-foreground",
                )}
              >
                {item.label}
                {item.badge && unread > 0 ? (
                  <span
                    className="absolute right-1.5 top-1.5 size-2 rounded-full bg-destructive"
                    aria-label={`${unread} unread notifications`}
                  />
                ) : null}
              </Link>
            ))}
          </nav>
          <span className="sm:hidden" aria-hidden />
        </div>
      </header>

      <main className="mx-auto w-full max-w-5xl flex-1 px-4 py-6 pb-24 sm:pb-10">
        {children}
      </main>

      <nav
        aria-label="Main"
        className="pb-safe fixed inset-x-0 bottom-0 z-40 border-t border-border bg-card sm:hidden"
      >
        <div className="grid grid-cols-5">
          {mobileNav.map((item) => {
            if (item.href === "__cta__") {
              return (
                <Link
                  key="cta"
                  href="/app/cases/new"
                  aria-label="Start a case"
                  className="flex items-center justify-center"
                >
                  <span className="flex size-14 -translate-y-3 items-center justify-center rounded-full bg-primary text-primary-foreground shadow-lg ring-4 ring-card">
                    <PlusIcon className="size-6" aria-hidden />
                  </span>
                </Link>
              );
            }
            const Icon = item.icon;
            const active = isActive(pathname, item.href, item.exact);
            return (
              <Link
                key={item.href}
                href={item.href}
                aria-current={active ? "page" : undefined}
                className={cn(
                  "relative flex min-h-14 flex-col items-center justify-center gap-0.5 py-2 text-[0.65rem] font-medium",
                  active ? "text-primary" : "text-muted-foreground",
                )}
              >
                <span className="relative">
                  <Icon className="size-5" aria-hidden />
                  {item.badge && unread > 0 ? (
                    <span
                      className="absolute -right-1 -top-0.5 size-2 rounded-full bg-destructive"
                      aria-label={`${unread} unread notifications`}
                    />
                  ) : null}
                </span>
                {item.label}
              </Link>
            );
          })}
        </div>
      </nav>
    </div>
  );
}
