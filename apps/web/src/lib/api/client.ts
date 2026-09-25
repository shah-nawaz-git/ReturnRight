import type { ProblemDetails } from "./types";

export const UNREACHABLE_MESSAGE =
  "We couldn't reach ReturnRight. Check your connection and try again.";

export class ApiError extends Error {
  readonly status: number;
  readonly problem: ProblemDetails | null;

  constructor(status: number, problem: ProblemDetails | null) {
    super(problem?.title ?? `API request failed with status ${status}`);
    this.name = "ApiError";
    this.status = status;
    this.problem = problem;
  }

  get code(): string | undefined {
    return this.problem?.code;
  }
}

let csrfToken: string | null = null;
let csrfRequest: Promise<string> | null = null;

async function fetchCsrfToken(): Promise<string> {
  const res = await fetch("/api/auth/csrf", { credentials: "include" }).catch(() => null);
  if (!res) {
    throw new ApiError(0, null);
  }
  if (!res.ok) {
    throw new ApiError(res.status, await parseProblem(res));
  }
  const body = (await res.json()) as { token: string };
  csrfToken = body.token;
  return body.token;
}

async function getCsrfToken(): Promise<string> {
  if (csrfToken !== null) {
    return csrfToken;
  }
  csrfRequest ??= fetchCsrfToken().finally(() => {
    csrfRequest = null;
  });
  return csrfRequest;
}

export function resetCsrfToken(): void {
  csrfToken = null;
}

function asProblem(body: unknown): ProblemDetails | null {
  if (
    body &&
    typeof body === "object" &&
    typeof (body as ProblemDetails).title === "string"
  ) {
    return body as ProblemDetails;
  }
  return null;
}

async function parseProblem(res: Response): Promise<ProblemDetails | null> {
  const contentType = res.headers.get("content-type") ?? "";
  if (!contentType.includes("json")) {
    // Plain-text/HTML error bodies (e.g. a proxy 500) are not for display.
    await res.text().catch(() => null);
    return null;
  }
  return asProblem(await res.json().catch(() => null));
}

async function rawFetch(path: string, init: RequestInit): Promise<Response> {
  const method = (init.method ?? "GET").toUpperCase();
  const headers = new Headers(init.headers);
  if (method !== "GET" && method !== "HEAD") {
    headers.set("X-CSRF-TOKEN", await getCsrfToken());
  }
  return fetch(path, { ...init, headers, credentials: "include" });
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  let res: Response;
  try {
    res = await rawFetch(path, init);
  } catch {
    // Network failure (DNS, CORS, connection refused) — fetch throws TypeError.
    throw new ApiError(0, null);
  }

  // Stale CSRF token (e.g. after re-login) — refresh once and retry.
  if (res.status === 400) {
    const problem = await parseProblem(res);
    if (problem?.code === "csrf_invalid") {
      resetCsrfToken();
      res = await rawFetch(path, init);
    } else {
      throw new ApiError(res.status, problem);
    }
  }

  if (!res.ok) {
    throw new ApiError(res.status, await parseProblem(res));
  }
  if (res.status === 204) {
    return undefined as T;
  }
  return (await res.json()) as T;
}

/** Multipart upload with progress (evidence, attachments, intakes, proofs). */
export function uploadFile<T>(
  path: string,
  formData: FormData,
  onProgress?: (fraction: number) => void,
): Promise<T> {
  return getCsrfToken().then(
    (token) =>
      new Promise<T>((resolve, reject) => {
        const xhr = new XMLHttpRequest();
        xhr.open("POST", path);
        xhr.withCredentials = true;
        // Never set Content-Type for multipart — the browser adds the boundary.
        xhr.setRequestHeader("X-CSRF-TOKEN", token);
        xhr.upload.onprogress = (event) => {
          if (event.lengthComputable && onProgress) {
            onProgress(event.loaded / event.total);
          }
        };
        xhr.onload = () => {
          const contentType = xhr.getResponseHeader("content-type") ?? "";
          const body = contentType.includes("json") && xhr.responseText
            ? JSON.parse(xhr.responseText)
            : xhr.responseText || null;
          if (xhr.status >= 200 && xhr.status < 300) {
            resolve(body as T);
          } else {
            reject(new ApiError(xhr.status, asProblem(body)));
          }
        };
        xhr.onerror = () => reject(new ApiError(0, null));
        xhr.send(formData);
      }),
  );
}

/** Problem+json helpers for forms. */
export function problemTitle(error: unknown, fallback = "Something went wrong."): string {
  if (error instanceof ApiError) {
    if (error.problem?.title) return error.problem.title;
    if (error.status === 0 || error.status >= 500) return UNREACHABLE_MESSAGE;
    return fallback;
  }
  if (error instanceof TypeError) return UNREACHABLE_MESSAGE;
  return fallback;
}

export function fieldErrors(error: unknown): Record<string, string> {
  if (error instanceof ApiError && error.problem?.errors) {
    return Object.fromEntries(
      Object.entries(error.problem.errors).map(([k, v]) => [
        k.charAt(0).toLowerCase() + k.slice(1),
        v[0] ?? "Invalid value",
      ]),
    );
  }
  return {};
}
