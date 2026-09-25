import { render, screen } from "@testing-library/react";
import { describe, expect, it } from "vitest";
import Home from "@/app/page";

describe("Home page", () => {
  it("renders the ReturnRight heading", () => {
    render(<Home />);
    expect(
      screen.getByRole("heading", { name: "ReturnRight" }),
    ).toBeInTheDocument();
  });
});
