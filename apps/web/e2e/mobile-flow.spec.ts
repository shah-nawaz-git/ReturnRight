import { expect, test, type Page } from "@playwright/test";
import path from "node:path";

import { FIXTURES, pickFile, registerUser, waitForContent } from "./helpers";

test.skip(({ isMobile }) => !isMobile, "mobile project only");

async function expectNoHorizontalOverflow(page: Page) {
  // Step transitions animate x-translate for ~180ms; poll past the transient.
  await expect
    .poll(
      () => page.evaluate(() => document.documentElement.scrollWidth),
      { timeout: 3_000, intervals: [250, 500, 1_000] },
    )
    .toBeLessThanOrEqual(390);
}

test("mobile layout: no overflow across the flow, bottom nav works", async ({
  page,
}) => {
  test.setTimeout(180_000);
  await registerUser(page);

  // Landing
  await page.goto("/");
  await waitForContent(page);
  await expectNoHorizontalOverflow(page);

  // Home
  await page.goto("/app");
  await waitForContent(page);
  await expectNoHorizontalOverflow(page);
  // Bottom navigation is visible on mobile.
  const bottomNav = page.locator("nav[aria-label='Main']").last();
  await expect(bottomNav).toBeVisible();
  await expect(
    bottomNav.getByRole("link", { name: "Start a case" }),
  ).toHaveAttribute("href", "/app/cases/new");

  // Wizard step 1
  await page.goto("/app/cases/new");
  await page.evaluate(() => window.sessionStorage.clear());
  await page.reload();
  await waitForContent(page);
  await expectNoHorizontalOverflow(page);
  await page.getByRole("radio", { name: /Damaged item/ }).click();
  await page.getByRole("button", { name: "Next" }).click();

  await page.getByRole("radio", { name: /^Full refund/ }).click();
  await page.locator("#outcome-amount").fill("389.99");
  await page.getByRole("button", { name: "Next" }).click();

  // Wizard step 3 — upload a photo as the receipt (camera trigger can't be
  // automated; "Choose file" covers the same intake path).
  await expectNoHorizontalOverflow(page);
  await pickFile(page, "Choose file", path.join(FIXTURES, "damage.png"));

  // Extraction likely fails for a synthetic image -> notice, then manual entry.
  await expect(
    page.getByText(/couldn.t read this document automatically/i),
  ).toBeVisible({ timeout: 60_000 });
  await page.getByRole("button", { name: "Next" }).click();

  // Wizard step 4
  await expect(
    page.getByRole("heading", { name: "Enter the purchase details" }),
  ).toBeVisible();
  await expectNoHorizontalOverflow(page);
  await page.locator("#wiz-merchant").fill("SoundMarket");
  await page.locator("#wiz-total").fill("389.99");
  await page.locator("#wiz-item-0-name").fill("Auralis X4 Headphones");
  await page.getByRole("button", { name: "Next" }).click();

  await page
    .locator("#description")
    .fill("The headphones arrived with a cracked headband.");
  await page.getByRole("button", { name: "Create case" }).click();
  await page.waitForURL(/\/app\/cases\/[0-9a-f-]{36}/, { timeout: 30_000 });
  await waitForContent(page);
  await expectNoHorizontalOverflow(page);

  // Record update via the header button.
  await page.getByRole("button", { name: "Record update" }).first().click();
  const dialog = page.getByRole("dialog");
  await expect(dialog).toBeVisible();
  await dialog.locator("#iu-note").fill("Sent the seller a message.");
  await dialog.getByRole("button", { name: "Record update" }).click();
  await expect(
    dialog.getByRole("button", { name: "Save again" }),
  ).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.getByText("Seller contacted").first()).toBeVisible();
});
