# UX

Design notes for the consumer frontend (`apps/web`). The audience is a
stressed person who bought something that went wrong — not a support agent —
so the interface optimizes for calm and for the next concrete step.

## Design principles

- **Resolution-first.** Every screen answers "what do I do now?" The case
  workspace leads with a Next step card, not a dashboard of stats.
- **Calm and trustworthy.** Muted palette, generous whitespace, no badges of
  honor, no urgency colors unless something is genuinely overdue.
- **Mobile-first.** Most evidence is a phone photo taken next to the damaged
  box; the app is designed at 390 px first and enhanced upward.
- **Plain language.** "Tell the seller about the problem", "Check whether
  your refund arrived" — no legal or CRM vocabulary.
- **Never judgmental.** Readiness is "5 of 6 details organized", not a score
  or a grade. Missing items are phrased as what to add, never what the user
  did wrong.
- **Unknown is a legitimate state.** Dates and amounts show "—" rather than
  invented placeholders; extracted values are marked "check this" instead of
  being silently trusted.
- **No legal claims.** The UI never says the user is entitled to an outcome
  and never computes statutory deadlines.

## Tokens

From `apps/web/src/app/globals.css` (`:root`):

| Token | Value | Use |
| --- | --- | --- |
| `--background` | `#f7f8f6` | app background, warm gray-green |
| `--card` / `--popover` | `#ffffff` | surfaces |
| `--foreground` | `#17212b` | body text |
| `--muted` | `#eef0ec` | fills, progress track |
| `--muted-foreground` | `#5d6572` | secondary text (darkened for AA) |
| `--primary` / `--ring` | `#315c6b` | actions, focus ring — desaturated teal |
| `--secondary` / `--accent` | `#eef0ec` / `#e8ebe5` | quiet fills |
| `--success` | `#2b7354` | resolved states (darkened for AA) |
| `--warning` | `#8f6210` | needs-attention, "check this" (darkened for AA) |
| `--destructive` | `#b54747` | delete, overdue |
| `--border` / `--input` | `#e4e7ec` | hairlines |
| `--radius` | `0.625rem` | base radius; cards use `2xl`, controls `xl` |

`muted-foreground`, `success` and `warning` were darkened from the original
palette during the axe pass because their first values failed WCAG AA
contrast — accessibility drove the palette, not the other way around.

Typography: **Inter** for everything (`--font-sans`); **Geist Mono** only for
order/reference numbers where alignment matters. Headings use
`tracking-tight`; no display font, no italics. Shadows are minimal — one
`shadow-lg` on the mobile "+", everything else is hairline borders.

## Components

Shared components in `src/components`:

- `file-drop-zone` — React Aria `DropZone` + two `FileTrigger`s (file picker,
  mobile camera), client-side pre-validation matching the API's messages,
  upload progress with an sr-only `role="status"` announcer.
- `status-badge` — case status chip; label + color, never color alone.
- `date-with-source` — date plus provenance ("From your invoice · confirmed"
  / "check this" in warning color).
- `confirm-dialog` — shared destructive-action confirm (delete case,
  discard draft, delete account).
- `empty-state` — dashed-border card: icon, one-line title, one line of
  guidance, optional actions.
- `inline-error` — problem-details renderer with retry; used in place of raw
  errors everywhere.
- `form-field` — label + `aria-invalid`/`aria-describedby` wiring + hint and
  error slots.
- `case-card` — list row: title, merchant, status badge, relative follow-up.
- `intake-uploader` — wraps the drop zone with intake lifecycle: "Reading
  your document…", the failed-extraction notice, "Use a different file".
- `purchase-form` — merchant/order/date/currency/total + item rows, with
  provenance tags.
- Case workspace (`components/case-workspace/`): `NextStepCard` (accent-bar
  card driven by the next-action API), `ReadinessCard` ("N of M details
  organized" + per-item guidance), `TimelineList` (day-grouped events),
  `EvidenceGrid` (clickable tiles: type label over file name), `InteractionsList`,
  `FollowUpsList`, `SummaryCard`, and the six dialogs (Record update, Add
  evidence, Schedule follow-up, Change status, Edit case, Resolve).

## Navigation

- **Desktop:** top bar — Home, My cases, Purchases, Notifications (unread
  dot), Profile; active item gets a muted pill.
- **Mobile:** fixed bottom bar — Home | Cases | **+** | Alerts | Profile.
  The raised center + goes straight to `/app/cases/new`; safe-area padding
  via `env(safe-area-inset-bottom)`.
- **Focused flows:** the case wizard and purchase form live in an
  `(focused)` route group with no shell nav — just the logo and a close
  button — so a multi-step flow has no escape hatches that lose state by
  accident (closing a dirty wizard asks first).

## Responsive rules

- Tested at 390×844, 768×1024 and 1440×900. The workspace switches to two
  columns (`lg:grid-cols-[2fr_1fr]`) only at `lg`.
- Case workspace stacking on mobile: breadcrumb → header → collapsible
  "Purchase & request" `<details>` summary strip → resolved banner →
  Next step → Readiness → Timeline → Evidence → Interactions → Follow-ups.
  The full Summary card stays inside the `<details>` on mobile and becomes a
  sticky side column on desktop.
- Buttons in action rows go full-width stacked below `sm` (`flex-col
  sm:flex-row`); every interactive element has a ≥44 px target on small
  screens (`.touch-target` = `max-sm:min-h/w-11`, icon buttons are `size-11`).
- `document.documentElement.scrollWidth <= viewport` is asserted in the
  mobile E2E spec — no horizontal scroll allowed.
- Dialogs are capped at `max-h-[85dvh]` with internal scroll on mobile.

## Major flows and rationale

### The 5-step case wizard

What went wrong → what outcome → receipt upload → purchase details →
affected items + description. A wizard (not one long form) because each step
is one decision, the steps before upload feed the extractor's context, and
per-step focus management (`tabIndex={-1}` heading focus + `aria-live` step
announcement) keeps keyboard users oriented. State persists to
`sessionStorage`, so a reload mid-flow loses nothing; closing with unsaved
input confirms first.

Extraction failure is a designed outcome, not an error page: the wizard stays
on the upload step, shows "We couldn't read this document automatically. You
can still enter the purchase details manually.", and Next continues to blank
manual entry with the file retained as purchase proof. Succeeded extraction
prefills step 4 under "Check the purchase details" with per-field provenance
tags — "From your document" (muted) or "Check this" (warning) — and editing
a field marks it user-edited.

### The case workspace

One screen answering five questions: *What should I do next?* (Next step
card with one action button), *How prepared am I?* (Readiness), *What
happened so far?* (day-grouped timeline), *What do I have to prove it?*
(evidence grid), *When do I check back?* (follow-ups). The header's primary
action is **Record update**, not Resolve — most visits are logging progress,
and resolution lives in the overflow menu so it's deliberate.

### Feedback and copy

- Empty states are instructions, not dead ends — "No cases yet / If something
  has gone wrong with a purchase, create a case and keep everything in one
  place." with a Start-a-case action; "Nothing here yet / We'll let you know
  when a follow-up is due or something needs your attention."
- Field errors are inline and per-field (`FormField` + RFC 7807 `errors`
  dictionary); transient success/failure uses a toast ("Your case is ready.",
  "Case reopened."); unreachable-API failures collapse to "We couldn't reach
  ReturnRight. Check your connection and try again."
- Destructive actions state the blast radius: "This permanently removes the
  case, its timeline, evidence files and follow-ups. Linked purchases and
  documents stay."
- Motion: only step transitions and small state changes (0.18 s fade/slide);
  `MotionConfig reducedMotion="user"` disables them for users who prefer
  reduced motion. No looping or decorative animation.

## Accessibility

- Semantic landmarks (`nav`/`main`/`section` with `aria-labelledby`), one
  `h1` per view, visible focus ring via a global `:focus-visible` rule.
- Form fields get labels + `aria-describedby`/`aria-invalid` from
  `FormField`; icon-only buttons have `aria-label` ("More actions", "Remove
  file", "Edit update", "Delete update").
- Wizard focus moves to the step `h1` (`tabIndex={-1}`, no focus outline on
  the non-interactive element) while an `sr-only` live region announces
  "Step n of 5". Upload progress and extraction status use `role="status"`.
- Status and severity are never color-only: badges carry text, the unread
  dot has an `aria-label`, readiness items are listed in words.
- Dialogs trap focus and return it on close — verified manually (Tab to
  Record update → focus inside → Escape → focus returns) and in
  `e2e/interactions.spec.ts`.
- **axe** (`@axe-core/playwright`, `e2e/a11y.spec.ts`): zero serious/critical
  violations on `/`, `/login`, `/register`, `/app`, `/app/cases`,
  `/app/cases/new`, `/app/cases/[id]`, `/app/purchases`,
  `/app/notifications`, `/app/profile`.

## Browser QA performed

Inspected in system Chrome at 390×844, 768×1024 and 1440×900: landing,
login, register, home, wizard steps 1/3/4 (with a real invoice upload),
case workspace, resolved case (banner + reopen), Record update and Resolve
dialogs, purchases, purchase detail, notifications, profile, and empty
states. No horizontal overflow on any size; dialogs fit 390×844. Reference
captures are in [`docs/screenshots/`](screenshots/).
