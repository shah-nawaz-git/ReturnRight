import { expect, request, test } from "@playwright/test";
import fs from "node:fs";
import path from "node:path";

import { apiPost, apiUpload, csrfToken, FIXTURES, registerOn, waitForContent } from "./helpers";

const BASE = process.env.PW_BASE_URL || "http://localhost:3000";

// API-level checks don't depend on viewport; run them on the desktop project
// only (keeps auth-rate-limit pressure and suite time down).
test.skip(({ isMobile }) => Boolean(isMobile), "API-level, desktop only");

async function newUserContext() {
  const ctx = await request.newContext({ baseURL: BASE });
  const creds = await registerOn(ctx);
  return { ctx, creds };
}

test.describe("api security", () => {
  test("user B cannot reach user A's resources (404, not 403)", async () => {
    test.setTimeout(120_000);
    const a = await newUserContext();
    const b = await newUserContext();
    try {
      // A creates a purchase with a document, a case, evidence, an interaction.
      const purchaseRes = await apiPost(a.ctx, "/api/purchases", {
        merchantName: "Victim Shop",
        currency: "EUR",
        items: [{ productName: "Widget", quantity: 1, unitPrice: 5 }],
      });
      expect(purchaseRes.status()).toBe(201);
      const purchase = (await purchaseRes.json()) as { id: string };

      const pdf = fs.readFileSync(path.join(FIXTURES, "unreadable.pdf"));
      const docRes = await apiUpload(a.ctx, `/api/purchases/${purchase.id}/documents`, {
        file: { name: "invoice.pdf", mimeType: "application/pdf", buffer: pdf },
      });
      expect(docRes.status(), await docRes.text()).toBe(201);
      const doc = (await docRes.json()) as { id: string };

      const caseRes = await apiPost(a.ctx, "/api/cases", {
        problemType: "DamagedItem",
        description: "Arrived cracked, want my money back please.",
        requestedOutcomeType: "FullRefund",
        requestedAmount: 5,
        requestedCurrency: "EUR",
        purchaseId: purchase.id,
      });
      expect(caseRes.status()).toBe(201);
      const kase = (await caseRes.json()) as { id: string };

      const png = fs.readFileSync(path.join(FIXTURES, "damage.png"));
      const evRes = await apiUpload(a.ctx, `/api/cases/${kase.id}/evidence`, {
        evidenceType: "DamagePhoto",
        file: { name: "damage.png", mimeType: "image/png", buffer: png },
      });
      expect(evRes.status(), await evRes.text()).toBe(201);

      const ixRes = await apiPost(a.ctx, `/api/cases/${kase.id}/interactions`, {
        interactionType: "ContactedSeller",
        occurredAt: new Date().toISOString(),
        note: "Sent a first message.",
      });
      expect(ixRes.status(), await ixRes.text()).toBe(201);
      const interaction = (await ixRes.json()) as { id: string };

      // B tries every route — must get 404, never 403 (no existence leak).
      const notFound: [string, string][] = [
        ["GET", `/api/cases/${kase.id}`],
        ["GET", `/api/documents/${doc.id}`],
        ["GET", `/api/cases/${kase.id}/case-file`],
        ["GET", `/api/purchases/${purchase.id}`],
      ];
      for (const [method, p] of notFound) {
        const res = await b.ctx.fetch(p, { method });
        expect(res.status(), `${method} ${p}`).toBe(404);
      }

      const bCsrf = await csrfToken(b.ctx);
      // Valid bodies — validation would 400 before the ownership check otherwise.
      const mutating: [string, string, unknown?][] = [
        [
          "PATCH",
          `/api/cases/${kase.id}`,
          {
            problemType: "WrongItemReceived",
            description: "hijacked description edit",
            requestedOutcomeType: "PartialRefund",
          },
        ],
        [
          "PATCH",
          `/api/cases/${kase.id}/interactions/${interaction.id}`,
          {
            interactionType: "SellerResponded",
            occurredAt: new Date().toISOString(),
            note: "hijacked",
          },
        ],
      ];
      for (const [method, p, body] of mutating) {
        const res = await b.ctx.fetch(p, {
          method,
          data: body,
          headers: { "X-CSRF-TOKEN": bCsrf },
        });
        expect(res.status(), `${method} ${p}`).toBe(404);
      }
      // DELETE and multipart POST via fetch()
      const del = await b.ctx.fetch(`/api/cases/${kase.id}`, {
        method: "DELETE",
        headers: { "X-CSRF-TOKEN": bCsrf },
      });
      expect(del.status()).toBe(404);

      const evPost = await b.ctx.fetch(`/api/cases/${kase.id}/evidence`, {
        method: "POST",
        headers: { "X-CSRF-TOKEN": bCsrf },
        multipart: {
          evidenceType: "DamagePhoto",
          file: { name: "damage.png", mimeType: "image/png", buffer: png },
        },
      });
      expect(evPost.status()).toBe(404);
    } finally {
      await a.ctx.dispose();
      await b.ctx.dispose();
    }
  });

  test("anonymous requests are rejected with 401", async () => {
    test.setTimeout(120_000);
    const anon = await request.newContext({ baseURL: BASE });
    try {
      for (const p of [
        "/api/cases",
        "/api/purchases",
        "/api/home",
        "/api/notifications",
      ]) {
        const res = await anon.get(p);
        expect(res.status(), `GET ${p}`).toBe(401);
      }
    } finally {
      await anon.dispose();
    }
  });

  test("mutating without a CSRF token is rejected", async () => {
    test.setTimeout(120_000);
    const a = await newUserContext();
    try {
      const res = await a.ctx.post("/api/cases", {
        data: {
          problemType: "DamagedItem",
          description: "No CSRF token attached to this request at all.",
          requestedOutcomeType: "FullRefund",
        },
      });
      expect(res.status()).toBe(400);
      expect((await res.json()).code).toBe("csrf_invalid");
    } finally {
      await a.ctx.dispose();
    }
  });

  test("rejects an executable disguised as pdf by magic bytes", async () => {
    test.setTimeout(120_000);
    const a = await newUserContext();
    try {
      const exe = Buffer.concat([Buffer.from("MZ"), Buffer.alloc(4096)]);
      const res = await apiUpload(a.ctx, "/api/intakes", {
        file: { name: "evil.pdf", mimeType: "application/pdf", buffer: exe },
      });
      expect(res.status()).toBe(400);
      const text = await res.text();
      expect(text).toContain("We can only accept PDF, JPG and PNG files.");
    } finally {
      await a.ctx.dispose();
    }
  });

  test("declared content type contradicting magic bytes is rejected", async () => {
    test.setTimeout(120_000);
    const a = await newUserContext();
    try {
      const pdf = fs.readFileSync(path.join(FIXTURES, "unreadable.pdf"));
      const res = await apiUpload(a.ctx, "/api/intakes", {
        file: { name: "receipt.pdf", mimeType: "image/png", buffer: pdf },
      });
      expect(res.status()).toBe(400);
      expect(await res.text()).toContain("The file type doesn't match its contents.");
    } finally {
      await a.ctx.dispose();
    }
  });

  test("path traversal filename is sanitized on upload", async () => {
    test.setTimeout(120_000);
    const a = await newUserContext();
    try {
      const pdf = fs.readFileSync(path.join(FIXTURES, "unreadable.pdf"));
      const res = await apiUpload(a.ctx, "/api/intakes", {
        file: { name: "../../evil.pdf", mimeType: "application/pdf", buffer: pdf },
      });
      expect(res.status()).toBe(201);
      const body = (await res.json()) as { fileName: string };
      expect(body.fileName).toBe("evil.pdf");
    } finally {
      await a.ctx.dispose();
    }
  });

  test("oversized upload is rejected", async () => {
    test.setTimeout(90_000);
    const a = await newUserContext();
    try {
      const big = Buffer.concat([
        Buffer.from("%PDF-1.4\n"),
        Buffer.alloc(10 * 1024 * 1024 + 1),
      ]);
      const res = await apiUpload(a.ctx, "/api/intakes", {
        file: { name: "huge.pdf", mimeType: "application/pdf", buffer: big },
      });
      expect([400, 413]).toContain(res.status());
    } finally {
      await a.ctx.dispose();
    }
  });
});

test("xss payloads are stored and rendered as literal text", async ({ page }) => {
  test.setTimeout(120_000);
  const dialogs: string[] = [];
  page.on("dialog", (d) => {
    dialogs.push(d.message());
    void d.dismiss();
  });

  await registerOn(page.request);

  const purchaseRes = await apiPost(page.request, "/api/purchases", {
    merchantName: '"><svg onload=alert(1)>',
    currency: "EUR",
    items: [{ productName: "Widget", quantity: 1, unitPrice: 5 }],
  });
  expect(purchaseRes.status()).toBe(201);
  const purchase = (await purchaseRes.json()) as { id: string };

  const caseRes = await apiPost(page.request, "/api/cases", {
    problemType: "DamagedItem",
    description: "<img src=x onerror=alert(1)> broke on arrival and I want a refund.",
    requestedOutcomeType: "FullRefund",
    requestedAmount: 5,
    requestedCurrency: "EUR",
    purchaseId: purchase.id,
  });
  expect(caseRes.status()).toBe(201);
  const kase = (await caseRes.json()) as { id: string };

  const ixRes = await apiPost(page.request, `/api/cases/${kase.id}/interactions`, {
    interactionType: "Other",
    occurredAt: new Date().toISOString(),
    note: "<script>alert(1)</script>",
  });
  expect(ixRes.ok(), await ixRes.text()).toBeTruthy();

  await page.goto(`/app/cases/${kase.id}`);
  await waitForContent(page);

  // Merchant and interaction note render as literal text in the workspace.
  await expect(
    page.getByText('"><svg onload=alert(1)>', { exact: false }).first(),
  ).toBeVisible();
  await expect(
    page.getByText("<script>alert(1)</script>", { exact: false }),
  ).toBeVisible();
  // The case description is only editable via the Edit case dialog — it must
  // land as a plain input value, never parsed markup.
  const stored = (await (await page.request.get(`/api/cases/${kase.id}`)).json()) as {
    description: string;
  };
  expect(stored.description).toContain("<img src=x onerror=alert(1)>");
  await page.getByRole("button", { name: "More actions" }).click();
  await page.getByRole("menuitem", { name: "Edit case" }).click();
  await expect(page.locator("#ec-desc")).toHaveValue(
    "<img src=x onerror=alert(1)> broke on arrival and I want a refund.",
  );
  await page.keyboard.press("Escape");
  // Let any delayed onerror/onload handlers fire.
  await page.waitForTimeout(1500);
  expect(dialogs, "no alert/dialog should fire from stored payloads").toEqual([]);
});
