import AxeBuilder from "@axe-core/playwright";
import { expect, test } from "@playwright/test";

import { firstCaseId, loginAsDemo, waitForContent } from "./helpers";

async function seriousViolations(page: import("@playwright/test").Page, path: string) {
  const results = await new AxeBuilder({ page }).analyze();
  return results.violations
    .filter((v) => v.impact === "serious" || v.impact === "critical")
    .map((v) => `${path}: [${v.impact}] ${v.id} — ${v.nodes.map((n) => n.target.join(" ")).join(" | ")}`);
}

test("public pages have no serious accessibility violations", async ({ page }) => {
  test.setTimeout(120_000);
  const failures: string[] = [];
  for (const path of ["/", "/login", "/register"]) {
    await page.goto(path);
    await page.waitForSelector("h1", { timeout: 60_000 });
    failures.push(...(await seriousViolations(page, path)));
  }
  expect(failures).toEqual([]);
});

test("app pages have no serious accessibility violations", async ({ page }) => {
  test.setTimeout(240_000);
  await loginAsDemo(page);
  const caseId = await firstCaseId(page);
  const failures: string[] = [];
  for (const path of [
    "/app",
    "/app/cases",
    "/app/cases/new",
    `/app/cases/${caseId}`,
    "/app/purchases",
    "/app/notifications",
    "/app/profile",
  ]) {
    await page.goto(path);
    await waitForContent(page);
    failures.push(...(await seriousViolations(page, path)));
  }
  expect(failures).toEqual([]);
});
