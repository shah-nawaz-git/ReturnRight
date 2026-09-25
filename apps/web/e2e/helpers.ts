import type { Page } from "@playwright/test";

export const DEMO_USER = {
  email: "demo@returnright.local",
  password: "DemoPass12345",
};

/** Logs in via the API through the Next proxy; cookies land on the page context. */
export async function loginAsDemo(page: Page) {
  const csrf = await page.request.get("/api/auth/csrf");
  const { token } = (await csrf.json()) as { token: string };
  const login = await page.request.post("/api/auth/login", {
    data: DEMO_USER,
    headers: { "X-CSRF-TOKEN": token },
  });
  if (!login.ok()) {
    throw new Error(`demo login failed: ${login.status()} ${await login.text()}`);
  }
}

export async function firstCaseId(page: Page): Promise<string> {
  const res = await page.request.get("/api/cases?filter=all");
  const cases = (await res.json()) as { id: string }[];
  if (cases.length === 0) throw new Error("no seeded cases");
  return cases[0].id;
}

/** Wait until the page has rendered real content (skeletons gone, h1 present). */
export async function waitForContent(page: Page) {
  await page.waitForSelector("h1", { timeout: 60_000 });
  await page.locator("[data-slot=skeleton]").first().waitFor({ state: "detached", timeout: 60_000 }).catch(() => undefined);
}
