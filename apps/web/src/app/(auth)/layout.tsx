import Link from "next/link";

import { Logo } from "@/components/app-shell";

export default function AuthLayout({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex min-h-dvh flex-1 flex-col items-center justify-center px-4 py-10">
      <Link href="/" className="mb-6" aria-label="ReturnRight home">
        <Logo />
      </Link>
      <div className="w-full max-w-[420px]">{children}</div>
    </div>
  );
}
