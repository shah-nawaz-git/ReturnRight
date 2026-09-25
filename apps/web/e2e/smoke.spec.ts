import { expect, test } from "@playwright/test";

test("home page shows the ReturnRight heading", async ({ page }) => {
  await page.goto("/");
  await expect(
    page.getByRole("heading", { name: "ReturnRight" }),
  ).toBeVisible();
});
