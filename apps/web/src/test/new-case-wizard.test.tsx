import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { beforeEach, describe, expect, it, vi } from "vitest";

vi.mock("next/link", () => ({
  default: ({ href, children, ...rest }: { href: string; children: React.ReactNode }) => (
    <a href={href} {...rest}>{children}</a>
  ),
}));

vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: vi.fn(), replace: vi.fn(), back: vi.fn() }),
  useSearchParams: () => new URLSearchParams(),
  usePathname: () => "/app/cases/new",
}));

vi.mock("@/lib/api/hooks", () => ({
  useConfirmIntake: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCreatePurchase: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useCreateCase: () => ({ mutateAsync: vi.fn(), isPending: false }),
  usePurchase: () => ({ data: undefined }),
  useCreateIntake: () => ({ mutateAsync: vi.fn(), isPending: false }),
  useDeleteIntake: () => ({ mutateAsync: vi.fn() }),
  useIntake: () => ({ data: undefined }),
}));

import NewCasePage from "@/app/app/(focused)/cases/new/page";
import { WIZARD_STORAGE_KEY, initialWizardState } from "@/components/new-case/state";

function renderWizard(state?: Partial<typeof initialWizardState>) {
  if (state) {
    window.sessionStorage.setItem(
      WIZARD_STORAGE_KEY,
      JSON.stringify({ ...initialWizardState, ...state }),
    );
  }
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={qc}>
      <NewCasePage />
    </QueryClientProvider>,
  );
}

beforeEach(() => window.sessionStorage.clear());

describe("new case wizard", () => {
  it("step 1: cannot proceed without choosing a problem type", async () => {
    renderWizard();
    const next = screen.getByRole("button", { name: "Next" });
    expect(next).toBeDisabled();
    await userEvent.click(screen.getByRole("radio", { name: /Damaged item/ }));
    expect(screen.getByRole("button", { name: "Next" })).toBeEnabled();
  });

  it("step 2: cannot proceed without choosing an outcome", () => {
    renderWizard({ step: 2, problemType: "DamagedItem" });
    expect(screen.getByRole("button", { name: "Next" })).toBeDisabled();
  });

  it("step 2: choosing a refund reveals the amount field", async () => {
    renderWizard({ step: 2, problemType: "DamagedItem" });
    expect(screen.queryByLabelText(/^Amount/)).not.toBeInTheDocument();
    await userEvent.click(screen.getByRole("radio", { name: /Full refund/ }));
    expect(screen.getByLabelText(/^Amount/)).toBeInTheDocument();
  });

  it("step 2: refund amount must be greater than zero", async () => {
    renderWizard({ step: 2, problemType: "DamagedItem" });
    await userEvent.click(screen.getByRole("radio", { name: /Full refund/ }));
    const amount = screen.getByLabelText(/^Amount/);
    await userEvent.type(amount, "0");
    expect(screen.getByRole("button", { name: "Next" })).toBeDisabled();
    await userEvent.clear(amount);
    await userEvent.type(amount, "39.99");
    expect(screen.getByRole("button", { name: "Next" })).toBeEnabled();
  });

  it("step 5: description under 20 characters shows the minimum-length message", async () => {
    renderWizard({
      step: 5,
      problemType: "DamagedItem",
      outcomeType: "FullRefund",
      purchase: null,
    });
    const desc = screen.getByRole("textbox", { name: /what happened/i });
    await userEvent.type(desc, "too short");
    expect(
      screen.getByText(/at least 20 characters/i),
    ).toBeInTheDocument();
    expect(screen.getByRole("button", { name: "Create case" })).toBeDisabled();
  });
});
