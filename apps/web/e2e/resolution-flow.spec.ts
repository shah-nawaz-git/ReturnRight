import { expect, test, type Page } from "@playwright/test";
import path from "node:path";

import {
  apiPost,
  DEMO_USER,
  FIXTURES,
  loginOn,
  pickFile,
  registerUser,
  waitForContent,
} from "./helpers";

test.describe.configure({ mode: "serial" });

test.skip(({ isMobile }) => Boolean(isMobile), "desktop only");

function caseId(page: Page): string {
  const m = /\/app\/cases\/([0-9a-f-]{36})/.exec(page.url());
  if (!m) throw new Error(`not on a case page: ${page.url()}`);
  return m[1];
}

async function recordUpdate(
  page: Page,
  opts: { typeLabel?: string; note: string; expectedBy?: string },
) {
  await page.getByRole("button", { name: "Record update" }).first().click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  if (opts.typeLabel) {
    await dialog.locator("#iu-type").click();
    await page.getByRole("option", { name: opts.typeLabel }).click();
  }
  if (opts.expectedBy) {
    await dialog.locator("#iu-expected").fill(opts.expectedBy);
  }
  await dialog.locator("#iu-note").fill(opts.note);
  await dialog.getByRole("button", { name: "Record update" }).click();
  await expect(dialog.getByRole("button", { name: "Save again" })).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
}

async function openMore(page: Page, item: string | RegExp) {
  await page.getByRole("button", { name: "More actions" }).click();
  await page.getByRole("menuitem", { name: item }).click();
}

test("full resolution journey: intake -> evidence -> updates -> resolve -> case file", async ({
  page,
}) => {
  test.setTimeout(180_000);
  await registerUser(page);

  // --- Wizard ---
  await page.goto("/app/cases/new");
  await page.evaluate(() => window.sessionStorage.clear());
  await page.reload();
  await waitForContent(page);

  await page.getByRole("radio", { name: /Damaged item/ }).click();
  await page.getByRole("button", { name: "Next" }).click();

  await page.getByRole("radio", { name: /^Full refund/ }).click();
  await page.locator("#outcome-amount").fill("389.99");
  await page.getByRole("button", { name: "Next" }).click();

  await expect(
    page.getByRole("heading", { name: /receipt or order confirmation/ }),
  ).toBeVisible();
  await pickFile(page, "Choose file", path.join(FIXTURES, "invoice_clean.pdf"));

  // --- Step 4: extracted details ---
  await expect(
    page.getByRole("heading", { name: "Check the purchase details" }),
    "extraction should prefill step 4",
  ).toBeVisible({ timeout: 60_000 });
  await expect(page.locator("#wiz-merchant")).toHaveValue(/soundmarket/i);
  await expect(page.locator("#wiz-order")).toHaveValue("SM-48213");
  await expect(page.getByText("From your document").first()).toBeVisible();

  await page.locator("#wiz-merchant").fill("SoundMarket");
  await page.getByRole("button", { name: "Next" }).click();

  // --- Step 5 ---
  await expect(
    page.getByRole("heading", { name: /Which item is affected/ }),
  ).toBeVisible();
  const affectedCheckbox = page.getByRole("checkbox").first();
  await expect(affectedCheckbox).toBeChecked();
  await expect(page.getByText("Auralis X4 Headphones")).toBeVisible();
  await page
    .locator("#description")
    .fill("The headphones arrived with a cracked headband and rattling left cup.");
  await page.getByRole("button", { name: "Create case" }).click();

  await page.waitForURL(/\/app\/cases\/[0-9a-f-]{36}/, { timeout: 30_000 });
  await waitForContent(page);
  const id = caseId(page);
  await expect(page.getByRole("heading", { name: "Auralis X4 Headphones" })).toBeVisible();

  const readinessText = page.getByText(/\d+ of \d+ details organized/);
  await expect(readinessText).toBeVisible();
  const before = parseInt((await readinessText.innerText()).match(/^(\d+)/)![1], 10);

  // --- Add evidence ---
  await page.getByRole("button", { name: "Add evidence" }).first().click();
  const evDialog = page.getByRole("dialog");
  await expect(evDialog).toBeVisible();
  const chooser = page.waitForEvent("filechooser");
  await evDialog.getByRole("button", { name: "Choose file" }).click();
  await (await chooser).setFiles(path.join(FIXTURES, "damage.png"));
  await evDialog.locator("#ev-desc").fill("Photo of the cracked headband");
  await evDialog.getByRole("button", { name: "Add evidence" }).click();
  await expect(evDialog).toBeHidden();

  await expect(page.getByText("Photo added: damage.png")).toBeVisible();
  await expect(
    page.getByText(new RegExp(`${before + 1} of \\d+ details organized`)),
    "readiness count should increase",
  ).toBeVisible();

  // --- Record update: contacted seller ---
  await recordUpdate(page, { note: "Emailed them photos and asked for a refund." });
  await expect(page.getByText("Seller contacted").first()).toBeVisible();
  await expect(page.getByText("Status changed to Seller contacted")).toBeVisible();

  // --- Schedule follow-up (+3 days via quick chip) ---
  await openMore(page, "Schedule follow-up");
  const fuDialog = page.getByRole("dialog");
  await expect(fuDialog).toBeVisible();
  await fuDialog.getByRole("button", { name: "Check for seller reply" }).click();
  await fuDialog.getByRole("button", { name: "Schedule" }).click();
  await expect(fuDialog).toBeHidden();
  await expect(
    page.getByText("Check for seller reply", { exact: true }),
  ).toBeVisible();

  const reminders1 = await page.request.get(`/api/cases/${id}/reminders`);
  expect(reminders1.ok()).toBeTruthy();
  const scheduled = (await reminders1.json()) as { status: string; channel: string }[];
  expect(
    scheduled.filter((r) => r.status === "Scheduled"),
    "email + in-app reminders should both be scheduled",
  ).toHaveLength(2);

  // --- Change status -> Waiting for seller ---
  await openMore(page, "Change status");
  const csDialog = page.getByRole("dialog");
  await csDialog.getByRole("radio", { name: "Waiting for seller" }).click();
  await csDialog.getByRole("button", { name: "Change status" }).click();
  await expect(csDialog).toBeHidden();
  await expect(page.getByText("Waiting for seller").first()).toBeVisible();

  // --- Refund promised with expectedBy = today + 2 ---
  const expected = new Date();
  expected.setDate(expected.getDate() + 2);
  const expectedInput = expected.toISOString().slice(0, 10);
  const expectedLabel = new Intl.DateTimeFormat("en-GB", {
    day: "numeric",
    month: "short",
    year: "numeric",
  }).format(expected);
  await recordUpdate(page, {
    typeLabel: "Seller promised a refund",
    note: "Seller agreed to refund in full once they process it.",
    expectedBy: expectedInput,
  });
  await expect(page.getByText("Refund pending").first()).toBeVisible();
  await expect(
    page.getByText(`Expected by ${expectedLabel}`).last(),
  ).toBeVisible();

  // --- Seller responded ---
  await recordUpdate(page, {
    typeLabel: "Seller responded",
    note: "Refund arrived",
  });

  // --- Resolve ---
  await openMore(page, "Resolve case");
  const resDialog = page.getByRole("dialog");
  await expect(resDialog).toBeVisible();
  await resDialog.getByRole("radio", { name: /Full refund received/ }).click();
  await expect(resDialog.locator("#res-amount")).toHaveValue("389.99");
  await resDialog.getByRole("button", { name: "Resolve case" }).click();
  await expect(resDialog).toBeHidden();

  await expect(page.getByText(/Resolved — Full refund received/)).toBeVisible();

  // Reopen must be present in the More menu (open and inspect without clicking).
  await page.getByRole("button", { name: "More actions" }).click();
  await expect(page.getByRole("menuitem", { name: "Reopen" })).toBeVisible();
  await page.keyboard.press("Escape");

  // Resolving cancels pending reminders.
  const reminders2 = await page.request.get(`/api/cases/${id}/reminders`);
  const cancelled = (await reminders2.json()) as { status: string }[];
  expect(
    cancelled.every((r) => r.status === "Cancelled"),
    `expected all reminders cancelled, got ${JSON.stringify(cancelled)}`,
  ).toBeTruthy();

  // --- Case file download ---
  const pdf = await page.request.get(`/api/cases/${id}/case-file`);
  expect(pdf.status()).toBe(200);
  expect(pdf.headers()["content-type"]).toContain("application/pdf");
  const body = await pdf.body();
  expect(body.subarray(0, 4).toString()).toBe("%PDF");
  expect(body.length).toBeGreaterThan(5 * 1024);
  expect(pdf.headers()["content-disposition"]).toMatch(/filename=.*\.pdf/i);
});

test("fresh user cannot access another user's case", async ({ page }) => {
  test.setTimeout(120_000);
  await registerUser(page);
  const created = await apiPost(page.request, "/api/cases", {
    problemType: "DamagedItem",
    description: "An item arrived broken and I want my money back.",
    requestedOutcomeType: "FullRefund",
    requestedAmount: 10,
    requestedCurrency: "EUR",
  });
  expect(created.status()).toBe(201);
  const { id } = (await created.json()) as { id: string };

  // Demo user context
  const baseURL = process.env.PW_BASE_URL || "http://localhost:3000";
  const demo = await page.context().browser()!.newContext({ baseURL });
  await loginOn(demo.request, DEMO_USER);

  // Demo cannot see the fresh user's case.
  const denied = await demo.request.get(`/api/cases/${id}`);
  expect(denied.status()).toBe(404);

  // And the fresh user cannot see any of the demo user's cases.
  const demoCases = await demo.request.get("/api/cases?filter=all");
  const demoIds = ((await demoCases.json()) as { id: string }[]).map((c) => c.id);
  expect(demoIds.length).toBeGreaterThan(0);
  for (const demoId of demoIds) {
    const res = await page.request.get(`/api/cases/${demoId}`);
    expect(res.status(), `demo case ${demoId} should be invisible`).toBe(404);
  }
  await demo.close();
});
