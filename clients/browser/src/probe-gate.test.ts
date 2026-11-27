import { describe, expect, it } from "vitest";

// PROBE ONLY — deliberately failing test, on a throwaway branch.
// Purpose: prove the `client` CI job actually executes the browser checks and
// fails when one breaks (i.e. the gate is live), rather than dying at setup.
describe("ci gate probe", () => {
  it("deliberately fails so CI must go red", () => {
    expect(1 + 1).toBe(3);
  });
});
