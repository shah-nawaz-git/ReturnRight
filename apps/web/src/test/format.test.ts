import { describe, expect, it } from "vitest";

import { formatRelativeDue } from "@/lib/format";

const now = new Date("2026-09-24T10:00:00");

describe("formatRelativeDue", () => {
  it("Due today", () => {
    expect(formatRelativeDue("2026-09-24T18:30:00", now)).toBe("Due today");
  });
  it("Due tomorrow", () => {
    expect(formatRelativeDue("2026-09-25T09:00:00", now)).toBe("Due tomorrow");
  });
  it("Due in N days", () => {
    expect(formatRelativeDue("2026-09-27T09:00:00", now)).toBe("Due in 3 days");
  });
  it("Overdue by 1 day", () => {
    expect(formatRelativeDue("2026-09-23T09:00:00", now)).toBe("Overdue by 1 day");
  });
  it("Overdue by N days", () => {
    expect(formatRelativeDue("2026-09-22T09:00:00", now)).toBe("Overdue by 2 days");
  });
});
