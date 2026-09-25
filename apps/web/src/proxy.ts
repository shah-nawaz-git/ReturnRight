import type { NextRequest } from "next/server";
import { NextResponse } from "next/server";

// UX-level gate only — the API enforces real auth. Keeps logged-out users
// off /app and logged-in users off the auth pages.
export function proxy(request: NextRequest) {
  const { pathname, search } = request.nextUrl;
  const hasSession = request.cookies.has("rr.auth");

  if (pathname.startsWith("/app") && !hasSession) {
    const url = new URL("/login", request.url);
    url.searchParams.set("next", pathname + search);
    return NextResponse.redirect(url);
  }

  if ((pathname === "/login" || pathname === "/register") && hasSession) {
    return NextResponse.redirect(new URL("/app", request.url));
  }

  return NextResponse.next();
}

export const config = {
  matcher: ["/app/:path*", "/login", "/register"],
};
