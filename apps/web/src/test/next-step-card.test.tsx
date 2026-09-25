import { render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { describe, expect, it, vi } from "vitest";

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...rest}>{children}</a>
  ),
}));

import { NextStepCard, actionCtas } from "@/components/case-workspace/panels";
import type { CaseDetailResponse, NextActionType } from "@/lib/api/types";

function wrap(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

function caseWithAction(actionType: NextActionType): CaseDetailResponse {
  return {
    id: "c1",
    status: "Open",
    nextAction: {
      key: "k",
      kind: "k",
      title: "Do the thing",
      description: "Because reasons",
      actionType,
      followUpId: null,
      date: null,
      isDismissible: false,
    },
  } as unknown as CaseDetailResponse;
}

describe("NextStepCard", () => {
  it.each(Object.entries(actionCtas))("maps %s to a CTA", (actionType, label) => {
    wrap(
      <NextStepCard
        issueCase={caseWithAction(actionType as NextActionType)}
        onAction={vi.fn()}
      />,
    );
    expect(screen.getByRole("button", { name: new RegExp(label) })).toBeInTheDocument();
  });

  it("hides the CTA for actionType None", () => {
    wrap(<NextStepCard issueCase={caseWithAction("None")} onAction={vi.fn()} />);
    expect(screen.getByText("Do the thing")).toBeInTheDocument();
    expect(screen.queryByRole("button")).not.toBeInTheDocument();
  });
});
