export class ApiError extends Error {
  readonly status: number;
  readonly problem: unknown;

  constructor(status: number, problem: unknown) {
    super(`API request failed with status ${status}`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }
}

let csrfToken: string | null = null;
let csrfRequest: Promise<string> | null = null;

async function getCsrfToken(): Promise<string> {
  if (csrfToken !== null) {
    return csrfToken;
  }

  csrfRequest ??= fetch("/api/auth/csrf", { credentials: "include" })
    .then(async (res) => {
      if (!res.ok) {
        throw new ApiError(res.status, await parseProblem(res));
      }
      const body = (await res.json()) as { token: string };
      csrfToken = body.token;
      return body.token;
    })
    .finally(() => {
      csrfRequest = null;
    });

  return csrfRequest;
}

async function parseProblem(res: Response): Promise<unknown> {
  const contentType = res.headers.get("content-type") ?? "";
  if (contentType.includes("json")) {
    return res.json().catch(() => null);
  }
  return res.text().catch(() => null);
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const method = (init.method ?? "GET").toUpperCase();
  const headers = new Headers(init.headers);

  if (method !== "GET" && method !== "HEAD") {
    headers.set("X-CSRF-TOKEN", await getCsrfToken());
  }

  const res = await fetch(path, {
    ...init,
    headers,
    credentials: "include",
  });

  if (!res.ok) {
    throw new ApiError(res.status, await parseProblem(res));
  }

  if (res.status === 204) {
    return undefined as T;
  }

  return (await res.json()) as T;
}
