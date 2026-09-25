import { expect, test } from "@playwright/test";

import { firstCaseId, loginAsDemo } from "./helpers";

test.beforeEach(async ({ page }) => {
  // Login can hit the API's 10/min auth limit and retry after ~65s.
  test.setTimeout(120_000);
  await loginAsDemo(page);
});

test("choose file opens the file chooser", async ({ page }) => {
  await page.goto("/app/purchases/new");
  const chooser = page.waitForEvent("filechooser", { timeout: 10_000 });
  await page.getByRole("button", { name: "Choose file" }).click();
  await chooser;
});

test("take a photo opens the file chooser", async ({ page, isMobile }) => {
  test.skip(!isMobile, "camera button only renders on mobile");
  await page.goto("/app/purchases/new");
  const chooser = page.waitForEvent("filechooser", { timeout: 10_000 });
  await page.getByRole("button", { name: "Take a photo" }).click();
  await chooser;
});

test("record update dialog traps and returns focus", async ({ page }) => {
  test.setTimeout(90_000);
  const caseId = await firstCaseId(page);
  await page.goto(`/app/cases/${caseId}`);
  const trigger = page.getByRole("button", { name: "Record update" }).first();
  await trigger.focus();
  await page.keyboard.press("Enter");

  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  const focusInside = await page.evaluate(
    () => document.activeElement?.closest('[role="dialog"]') !== null,
  );
  expect(focusInside).toBe(true);

  await page.keyboard.press("Escape");
  await expect(dialog).toBeHidden();
  await expect(trigger).toBeFocused();
});
