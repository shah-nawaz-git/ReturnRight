import { afterEach, describe, expect, it, vi } from "vitest";

import { apiFetch, ApiError, problemTitle, UNREACHABLE_MESSAGE } from "@/lib/api/client";

function res(status: number, body: unknown, contentType = "application/json") {
  return new Response(
    typeof body === "string" ? body : JSON.stringify(body),
    { status, headers: { "content-type": contentType } },
  );
}

afterEach(() => vi.unstubAllGlobals());

describe("apiFetch error normalization", () => {
  it("maps a non-JSON 500 (upstream unreachable) to the friendly message", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(res(500, "Internal Server Error", "text/plain")),
    );
    const error = await apiFetch("/api/anything").catch((e) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect(problemTitle(error)).toBe(UNREACHABLE_MESSAGE);
  });

  it("maps a network TypeError to the friendly message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockRejectedValue(new TypeError("Failed to fetch")));
    const error = await apiFetch("/api/anything").catch((e) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(0);
    expect(problemTitle(error)).toBe(UNREACHABLE_MESSAGE);
  });

  it("keeps a real problem-details title", async () => {
    vi.stubGlobal(
      "fetch",
      vi.fn().mockResolvedValue(
        res(409, { title: "This purchase has linked cases.", code: "conflict" }),
      ),
    );
    const error = await apiFetch("/api/anything").catch((e) => e);
    expect(problemTitle(error)).toBe("This purchase has linked cases.");
  });

  it("maps a JSON body without a title on 503 to the friendly message", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue(res(503, { whatever: true })));
    const error = await apiFetch("/api/anything").catch((e) => e);
    expect(problemTitle(error)).toBe(UNREACHABLE_MESSAGE);
  });
});

describe("problemTitle", () => {
  it("status 0 and 5xx with no problem → unreachable", () => {
    expect(problemTitle(new ApiError(0, null))).toBe(UNREACHABLE_MESSAGE);
    expect(problemTitle(new ApiError(502, null))).toBe(UNREACHABLE_MESSAGE);
  });
});
