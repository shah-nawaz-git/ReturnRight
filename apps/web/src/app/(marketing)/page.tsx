import {
  CheckCircle2Icon,
  CircleIcon,
  ListChecksIcon,
  PackageSearchIcon,
  ShieldCheckIcon,
} from "lucide-react";
import Link from "next/link";

import { Button } from "@/components/ui/button";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Logo } from "@/components/app-shell";
import { StatusBadge } from "@/components/status-badge";

function CasePreview() {
  return (
    <Card className="mx-auto w-full max-w-[560px] text-left shadow-sm">
      <CardHeader className="border-b border-border pb-3">
        <div className="flex items-center justify-between gap-3">
          <div>
            <CardTitle className="text-base">Auralis X4 Headphones</CardTitle>
            <p className="mt-0.5 text-xs text-muted-foreground">SoundMarket · Damaged item</p>
          </div>
          <StatusBadge status="WaitingForSeller" />
        </div>
      </CardHeader>
      <CardContent className="space-y-4 pt-4">
        <div className="rounded-lg border-l-2 border-primary bg-muted/50 px-3 py-2.5">
          <p className="text-sm font-medium">Waiting for seller response</p>
          <p className="mt-0.5 text-xs text-muted-foreground">
            You last updated this case on 12 Sep. Follow up on 25 Sep.
          </p>
        </div>
        <div>
          <p className="mb-2 text-xs font-medium text-muted-foreground">5 of 6 details organized</p>
          <ul className="space-y-1.5 text-sm">
            {[
              ["Purchase proof", true],
              ["Order or reference number", true],
              ["Problem description", true],
              ["Requested outcome", true],
              ["Photos of the item", true],
              ["Seller contacted", false],
            ].map(([label, done]) => (
              <li key={label as string} className="flex items-center gap-2">
                {done ? (
                  <CheckCircle2Icon className="size-4 text-success" aria-hidden />
                ) : (
                  <CircleIcon className="size-4 text-muted-foreground" aria-hidden />
                )}
                <span className={done ? "" : "text-muted-foreground"}>{label}</span>
              </li>
            ))}
          </ul>
        </div>
      </CardContent>
    </Card>
  );
}

export default function MarketingPage() {
  return (
    <div className="flex flex-1 flex-col">
      <header className="mx-auto flex w-full max-w-[1100px] items-center justify-between px-4 py-5">
        <Logo />
        <nav aria-label="Account" className="flex items-center gap-2">
          <Button variant="ghost" className="touch-target" nativeButton={false} render={<Link href="/login" />}>
            Log in
          </Button>
          <Button className="touch-target" nativeButton={false} render={<Link href="/register" />}>
            Start a case
          </Button>
        </nav>
      </header>

      <main className="mx-auto w-full max-w-[1100px] flex-1 px-4">
        <section className="py-12 text-center sm:py-20">
          <p className="text-sm font-medium text-primary">For when a purchase goes wrong</p>
          <h1 className="mx-auto mt-3 max-w-2xl text-3xl font-bold tracking-tight text-foreground sm:text-5xl">
            Keep a purchase problem organized until it&apos;s resolved.
          </h1>
          <p className="mx-auto mt-4 max-w-xl text-base text-muted-foreground">
            Track evidence, seller messages and follow-ups in one calm place —
            so nothing slips through the cracks.
          </p>
          <div className="mt-8 flex items-center justify-center gap-3">
            <Button size="lg" className="touch-target h-11 px-6" nativeButton={false} render={<Link href="/register" />}>
              Start a case
            </Button>
            <Button
              variant="outline"
              size="lg"
              className="touch-target h-11 px-6"
              nativeButton={false}
              render={<Link href="/login" />}
            >
              Log in
            </Button>
          </div>
        </section>

        <section aria-labelledby="how-it-works" className="py-10 pt-12">
          <h2 id="how-it-works" className="text-center text-lg font-semibold">
            How it works
          </h2>
          <div className="mt-6 grid gap-4 sm:grid-cols-3">
            {[
              {
                icon: PackageSearchIcon,
                title: "Build",
                text: "Upload your receipt — we pre-fill the details.",
              },
              {
                icon: ListChecksIcon,
                title: "Track",
                text: "Record every seller message and piece of evidence.",
              },
              {
                icon: ShieldCheckIcon,
                title: "Resolve",
                text: "Follow-ups remind you until the case is closed.",
              },
            ].map(({ icon: Icon, title, text }) => (
              <div key={title} className="rounded-xl border border-border bg-card p-5 text-center">
                <Icon className="mx-auto size-6 text-primary" aria-hidden />
                <h3 className="mt-3 text-sm font-semibold">{title}</h3>
                <p className="mt-1 text-sm text-muted-foreground">{text}</p>
              </div>
            ))}
          </div>
        </section>

        <section className="py-10" aria-label="Example case">
          <CasePreview />
          <p className="mx-auto mt-3 max-w-[560px] text-center text-xs text-muted-foreground">
            A case workspace: next step, readiness and timeline in one place.
          </p>
        </section>

        <section className="mx-auto max-w-2xl py-10 text-center">
          <h2 className="text-lg font-semibold">What ReturnRight is not</h2>
          <p className="mt-3 text-sm leading-relaxed text-muted-foreground">
            It doesn&apos;t give legal advice and it doesn&apos;t email sellers
            on your behalf. Your documents stay private — they&apos;re yours,
            used only to keep your case organized.
          </p>
        </section>
      </main>

      <footer className="border-t border-border py-6 text-center text-xs text-muted-foreground">
        ReturnRight — keep a purchase problem organized until it&apos;s resolved.
      </footer>
    </div>
  );
}
