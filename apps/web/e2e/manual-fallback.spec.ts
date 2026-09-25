import { expect, test } from "@playwright/test";
import path from "node:path";

import { FIXTURES, pickFile, registerUser, waitForContent } from "./helpers";

test("unreadable document falls back to manual entry and is kept as proof", async ({
  page,
}) => {
  test.setTimeout(120_000);
  await registerUser(page);

  await page.goto("/app/cases/new");
  await page.evaluate(() => window.sessionStorage.clear());
  await page.reload();
  await waitForContent(page);

  await page.getByRole("radio", { name: /Damaged item/ }).click();
  await page.getByRole("button", { name: "Next" }).click();
  await page.getByRole("radio", { name: /^Full refund/ }).click();
  await page.locator("#outcome-amount").fill("19.95");
  await page.getByRole("button", { name: "Next" }).click();

  // Blank PDF -> extraction fails -> notice stays on step 3.
  await pickFile(page, "Choose file", path.join(FIXTURES, "unreadable.pdf"));
  await expect(
    page.getByText(/couldn.t read this document automatically/i),
  ).toBeVisible({ timeout: 60_000 });
  await expect(
    page.getByRole("heading", { name: /receipt or order confirmation/ }),
  ).toBeVisible();

  await page.getByRole("button", { name: "Next" }).click();

  // Step 4: manual entry, empty fields.
  await expect(
    page.getByRole("heading", { name: "Enter the purchase details" }),
  ).toBeVisible();
  await expect(page.locator("#wiz-merchant")).toHaveValue("");
  await expect(page.locator("#wiz-order")).toHaveValue("");

  await page.locator("#wiz-merchant").fill("ManuShop");
  await page.locator("#wiz-order").fill("MN-1024");
  await page.locator("#wiz-date").fill("2025-09-01");
  await page.locator("#wiz-total").fill("19.95");
  await page.locator("#wiz-item-0-name").fill("Wooden spoon");
  await page.getByRole("button", { name: "Next" }).click();

  await expect(
    page.getByRole("heading", { name: /Which item is affected/ }),
  ).toBeVisible();
  await page
    .locator("#description")
    .fill("The spoon arrived split down the middle and is unusable.");
  await page.getByRole("button", { name: "Create case" }).click();

  await page.waitForURL(/\/app\/cases\/[0-9a-f-]{36}/, { timeout: 30_000 });
  await waitForContent(page);

  // The uploaded document is retained as purchase proof on the case.
  await expect(page.getByText("Purchase proof added: unreadable.pdf")).toBeVisible();
  await expect(page.getByText("Purchase proof").first()).toBeVisible();
  // Merchant appears in the summary.
  await expect(page.getByText("ManuShop").first()).toBeVisible();
});
