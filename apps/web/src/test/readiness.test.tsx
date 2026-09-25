import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";

import { ReadinessCard } from "@/components/case-workspace/panels";
import type { CaseDetailResponse } from "@/lib/api/types";

const issueCase = {
  readiness: {
    completed: 2,
    total: 4,
    items: [
      { key: "CaseCreated", label: "Case created", isComplete: true, explanation: null },
      { key: "SellerContact", label: "Seller contacted", isComplete: true, explanation: null },
      {
        key: "PurchaseProof",
        label: "Purchase proof",
        isComplete: false,
        explanation: "A receipt or order confirmation helps sellers act faster.",
      },
      {
        key: "OutcomeSet",
        label: "Requested outcome",
        isComplete: false,
        explanation: "Tell the seller what you want them to do.",
      },
    ],
  },
} as unknown as CaseDetailResponse;

describe("ReadinessCard", () => {
  it("renders the count and complete/incomplete items with explanations", () => {
    const qc = new QueryClient();
    render(
      <QueryClientProvider client={qc}>
        <ReadinessCard issueCase={issueCase} onAction={vi.fn()} />
      </QueryClientProvider>,
    );

    expect(screen.getByText("2 of 4 details organized")).toBeInTheDocument();
    expect(screen.getByText("Case created")).toBeInTheDocument();
    expect(
      screen.getByText("A receipt or order confirmation helps sellers act faster."),
    ).toBeInTheDocument();
    expect(
      screen.getByText("Tell the seller what you want them to do."),
    ).toBeInTheDocument();
  });
});
