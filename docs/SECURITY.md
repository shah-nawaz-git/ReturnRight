# Security

How ReturnRight protects a single user's private purchase records, and what
was actually verified. Nothing here is aspirational — every control listed
exists in code and is cited.

## Scope and assumptions

ReturnRight is a single-tenant consumer app: users manage their own purchase
problems, and there is no admin role. The threat actors considered:

- **Other authenticated users** trying to reach someone else's data (IDOR).
- **Unauthenticated web attackers**: CSRF, XSS via stored content, abusive or
  disguised file uploads, credential stuffing against the auth endpoints.
- **Out of scope:** a compromised host or database, a malicious operator,
  denial-of-service at infrastructure level.

## Authentication

ASP.NET Core Identity (`Security/AuthSetup.cs`):

- Passwords hashed by Identity's default hasher (PBKDF2 with per-user salt).
- Policy: minimum 10 characters, unique email, no composition rules.
- Lockout: 5 failed attempts → 5-minute lockout
  (`Lockout.AllowedForNewUsers`, `MaxFailedAccessAttempts = 5`).
- Session cookie `rr.auth`: `HttpOnly`, `SameSite=Lax`, sliding 14 days,
  `Secure` when `Auth:RequireSecureCookie` is on (default `true`; compose
  sets it `false` only for plain-HTTP local dev).
- No redirect-to-login for API calls: expired sessions get a `401`
  `problem+json` ("Please sign in to continue."), not an HTML login page.
- `POST /api/auth/logout` signs out; `DELETE /api/profile` deletes the
  account **only after re-checking the password**, then cascade-removes all
  rows and deletes every stored file.

## CSRF

Every non-safe (`POST`/`PATCH`/`DELETE`/…) request under `/api/*` must carry
a valid antiforgery token (`Security/AntiforgeryMiddleware.cs`):

- `GET /api/auth/csrf` issues the token pair; the client sends it back as the
  `X-CSRF-TOKEN` header, validated against the `rr.csrf` cookie.
- The middleware applies to **all** mutating `/api/*` requests, including
  login and register — a forged cross-site POST fails before it reaches an
  endpoint.
- The token is bound to the identity, so the frontend re-fetches it after
  login/logout and retries once automatically on a `csrf_invalid` (400)
  response.

## Authorization

- Every query is scoped by the authenticated `CurrentUser` id — a `UserId`
  filter on the entity or on its owning case/purchase. No request body can
  supply a `UserId`; the contracts don't have one.
- Foreign resources return **404**, never 403 — existence is not disclosed.
- Covered resources: cases (GET/PATCH/DELETE, reopen, resolve), documents
  (view/download/delete), purchases, intakes, evidence, interactions,
  follow-ups, reminders, case files, notifications, profile.
- Enforced by `AuthorizationTests` (integration) and `e2e/security.spec.ts`,
  which exercises cross-user access on every resource type end to end.

## Files

Upload validation (`Storage/UploadValidator.cs`, `UploadHelper.cs`):

- Content signature allowlist — `%PDF-`, JPEG `FF D8 FF`, PNG magic. Names
  and declared types are never trusted.
- Declared `Content-Type` must be in the allowlist (`application/octet-stream`
  tolerated) and must not contradict the detected signature.
- Extension must match the detected type (`.pdf`/`.jpg`/`.jpeg`/`.png`).
- Empty files and files over 10 MB rejected with friendly 400 messages.
- `SafeFileName` strips path components, control characters and
  `"><:*?|` — `../../evil.pdf` becomes `evil.pdf`.

Storage and serving (`Storage/LocalFileStorage.cs`, `Features/Documents`):

- Files are stored under generated keys (`yyyy/MM/{guid}.ext`) on a private
  volume — never web-served statically, never addressable by name.
- `ResolvePath` rejects any key that escapes the storage root.
- Downloads are streamed through authenticated, owner-scoped endpoints with
  `X-Content-Type-Options: nosniff`, `Cache-Control: private, no-store`,
  `Content-Security-Policy: sandbox` (renders inline PDFs/images inert), and
  a `Content-Disposition` header with an RFC 5987-encoded filename.
- Deleting a document removes the DB row and the physical file; deleting an
  account removes all of them.
- Body limits at three layers: Kestrel `MaxRequestBodySize` = 11 MB with a
  mapped `413` problem; the Next.js proxy allows 12 MB
  (`proxyClientMaxBodySize`) so the API's own friendlier 400/413 reaches the
  user; `UploadValidator` enforces the real 10 MB cap.

## Rate limiting

`System.Threading.RateLimiting` (`Security/RateLimitingSetup.cs`):

- `auth` policy: 10 requests/minute **per IP** on login and register.
- `uploads` policy: 20 requests/minute **per user** (falls back to IP) on
  intake and evidence/document uploads.
- Rejections return a `429` problem. `UseForwardedHeaders` is enabled for
  `X-Forwarded-For`/`X-Forwarded-Proto`, but with the default
  `KnownProxies`/`KnownNetworks` (loopback only). Behind the Compose stack's
  Next.js container the forwarded IP is therefore ignored and the `auth`
  limit is effectively shared by all clients — see Known limitations.
- Limits are in-memory per instance — see Known limitations.

## Web app headers

`apps/web/next.config.ts` sets on all routes: `X-Content-Type-Options:
nosniff`, `Referrer-Policy: strict-origin-when-cross-origin`,
`X-Frame-Options: DENY`, `Permissions-Policy: camera=(self), microphone=(),
geolocation=()` (camera needed for photo capture). **No strict
Content-Security-Policy yet** — Next.js emits inline scripts that a strict
CSP would break; listed as a known limitation.

## Extractor

The FastAPI service is treated as internal-only (`services/extractor/app`):

- Optional shared-secret gate: when `EXTRACTOR_SHARED_SECRET` is set, the API
  sends `X-Extractor-Key` and requests without it get 401.
- No persistence — bytes are processed in memory, nothing is written.
- Own validation: 10 MB cap, kind detection (pdf/jpeg/png → 415 otherwise),
  max 5 OCR pages, 20 s per-page OCR timeout.
- Logs record file kind/size/page count and extracted **field names** — never
  the extracted values or document text.

## Logging and privacy

Log statements were inventoried. What is logged: reminder ids and statuses,
cleanup counts, storage/image document ids, SMTP subjects, exception
**type names** (not messages with payloads), extractor field names, and the
fixed demo email at seed time. `ReminderProcessor` redacts the recipient's
email address from persisted `LastError` strings. What is never logged:
passwords, user emails at Info level, tokens, file bytes, OCR text, or
document contents.

## Secrets

`.env` is gitignored; `.env.example` documents variable names only. The demo
password comes exclusively from `DEMO_PASSWORD`/`Seed__DemoPassword` — there
is no default credential in the repo. Gitleaks runs in CI (`.gitleaks.toml`,
`security` job) and was run locally over both the working tree and full
history.

## Verification performed

On 2026-09-25, against this codebase:

- **Integration tests** (Testcontainers PostgreSQL): `AuthTests`,
  `CsrfTests`, `RateLimitTests`, `AuthorizationTests`, `DocumentTests`,
  `AccountDeletionTests`, `IntakeTests`, `ReminderProcessingTests`,
  `NotificationTests`, `HomeEndpointTests` — plus unit tests
  `UploadValidatorTests`, `LocalFileStorageTests`, `ReminderRetryPolicyTests`.
- **E2E** (`e2e/security.spec.ts`, system Chrome): cross-user 404s on every
  resource type, anonymous 401, missing-CSRF 400, disguised-exe rejection,
  filename sanitization, oversize rejection, and stored XSS payloads
  (`<img onerror>`, `<script>`, `"><svg onload>`) rendered as inert text with
  no dialog firing.
- **Gitleaks** `git` (history) and `dir` scans: clean.
- **`npm audit --audit-level=high`**: 0 vulnerabilities.
- **`dotnet list package --vulnerable --include-transitive`**: none in any
  project.
- **`pip-audit`** on extractor prod + dev requirements: none found.

No penetration test was performed; this is self-reviewed verification, not an
external audit.

## Known V1 limitations

- **No strict CSP** on the web app (Next.js inline scripts).
- **No MFA** and no email verification or password-reset flow.
- **Local filesystem storage** behind `IFileStorage` — a lost volume means
  lost files; no encryption at rest beyond the host's.
- **No audit log of logins** — security events aren't recorded for review.
- **Rate limiting is in-memory per instance** — multi-instance deployments
  need a distributed limiter or a front proxy.
- **Per-IP auth limit is proxy-naive** — forwarded client IPs are only
  trusted from loopback proxies, so behind the Compose web container the
  login/registration limit is shared. Register the proxy in
  `ForwardedHeadersOptions.KnownNetworks` when deploying behind one.
- **Docker stack verified in CI only** — the compose file was exercised on
  GitHub Actions, not on a local Docker host.
