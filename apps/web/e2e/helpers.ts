import { expect, type APIRequestContext, type Page } from "@playwright/test";
import path from "node:path";

export const FIXTURES = path.join(__dirname, "fixtures");

export const DEMO_USER = {
  email: "demo@returnright.local",
  password: "DemoPass12345",
};

/** Logs in via the API through the Next proxy; cookies land on the page context. */
export async function loginAsDemo(page: Page) {
  const login = await postAuth(page.request, "/api/auth/login", DEMO_USER);
  if (!login.ok()) {
    throw new Error(`demo login failed: ${login.status()} ${await login.text()}`);
  }
}

/** Fetch a CSRF request token (cookie is set on the context automatically). */
export async function csrfToken(request: APIRequestContext): Promise<string> {
  const res = await request.get("/api/auth/csrf");
  expect(res.ok()).toBeTruthy();
  return (await res.json()).token as string;
}

/**
 * The API limits auth endpoints to 10 requests/minute per IP. An e2e suite can
 * exceed that, so retry once after the fixed window resets.
 */
async function postAuth(
  request: APIRequestContext,
  path: string,
  data: unknown,
) {
  let res = await request.post(path, {
    data,
    headers: { "X-CSRF-TOKEN": await csrfToken(request) },
  });
  if (res.status() === 429) {
    await new Promise((r) => setTimeout(r, 65_000));
    res = await request.post(path, {
      data,
      headers: { "X-CSRF-TOKEN": await csrfToken(request) },
    });
  }
  return res;
}

/** Register a fresh user on any request context; its session lands there. */
export async function registerOn(request: APIRequestContext) {
  const email = `e2e_${Date.now()}_${Math.random().toString(36).slice(2, 8)}@test.local`;
  const password = "E2ePass12345";
  const res = await postAuth(request, "/api/auth/register", { email, password });
  expect(res.ok(), `register failed: ${res.status()} ${await res.text()}`).toBeTruthy();
  return { email, password };
}

/** Log in on any request context (retries once on 429). */
export async function loginOn(
  request: APIRequestContext,
  creds: { email: string; password: string },
) {
  const res = await postAuth(request, "/api/auth/login", creds);
  expect(res.ok(), `login failed: ${res.status()} ${await res.text()}`).toBeTruthy();
}

/** Register a fresh user via the API and keep its session on the page's context. */
export async function registerUser(page: Page) {
  return registerOn(page.request);
}

/** POST JSON with a fresh CSRF token bound to the current session. */
export async function apiPost(request: APIRequestContext, path: string, body?: unknown) {
  const token = await csrfToken(request);
  return request.post(path, { data: body, headers: { "X-CSRF-TOKEN": token } });
}

/** multipart POST with CSRF. */
export async function apiUpload(
  request: APIRequestContext,
  path: string,
  multipart: Record<
    string,
    string | number | { name: string; mimeType: string; buffer: Buffer }
  >,
) {
  const token = await csrfToken(request);
  return request.post(path, { multipart, headers: { "X-CSRF-TOKEN": token } });
}

/** Open a FileTrigger by clicking the labelled button and set files. */
export async function pickFile(page: Page, button: string | RegExp, file: string) {
  const chooser = page.waitForEvent("filechooser");
  await page.getByRole("button", { name: button }).first().click();
  await (await chooser).setFiles(file);
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
