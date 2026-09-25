import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";

import { StatusBadge } from "@/components/status-badge";
import { statusLabels } from "@/lib/labels";
import type { CaseStatus } from "@/lib/api/types";

const statuses = Object.keys(statusLabels) as CaseStatus[];

describe("StatusBadge", () => {
  it.each(statuses)("renders text for %s", (status) => {
    render(<StatusBadge status={status} />);
    expect(screen.getByText(statusLabels[status])).toBeInTheDocument();
  });
});
