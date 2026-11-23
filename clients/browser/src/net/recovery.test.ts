import { describe, expect, it, vi } from "vitest";
import { BoundedBackoff, recoverSession, type RecoveryActions } from "./recovery";

function actions(overrides: Partial<RecoveryActions> = {}): RecoveryActions {
  return {
    ensureAuth: vi.fn(async () => true),
    hasFreshMap: vi.fn(() => true),
    lastAppliedTick: vi.fn(() => 100),
    lastTickAgeMs: vi.fn(() => 100),
    requestMapSnapshot: vi.fn(async () => undefined),
    requestSnapshot: vi.fn(async () => undefined),
    ...overrides
  };
}

describe("recoverSession", () => {
  it("recovers in auth -> map -> snapshot order when the map is stale", async () => {
    const calls: string[] = [];
    const a = actions({
      ensureAuth: vi.fn(async () => {
        calls.push("auth");
        return true;
      }),
      hasFreshMap: vi.fn(() => false),
      requestMapSnapshot: vi.fn(async () => {
        calls.push("map");
      }),
      requestSnapshot: vi.fn(async () => {
        calls.push("snapshot");
      })
    });

    expect(await recoverSession(a)).toBe("recovered");
    expect(calls).toEqual(["auth", "map", "snapshot"]);
    expect(a.requestSnapshot).toHaveBeenCalledWith(100);
  });

  it("requests a full snapshot when the last tick is old", async () => {
    const a = actions({ lastTickAgeMs: () => 60_000 });
    await recoverSession(a, { tickRetentionMs: 5_000 });
    expect(a.requestSnapshot).toHaveBeenCalledWith(null);
  });

  it("requests a full snapshot when no tick was applied", async () => {
    const a = actions({ lastAppliedTick: () => 0, lastTickAgeMs: () => 0 });
    await recoverSession(a);
    expect(a.requestSnapshot).toHaveBeenCalledWith(null);
  });

  it("stops before map or snapshot when auth fails", async () => {
    const a = actions({ ensureAuth: async () => false });
    expect(await recoverSession(a)).toBe("auth-failed");
    expect(a.requestMapSnapshot).not.toHaveBeenCalled();
    expect(a.requestSnapshot).not.toHaveBeenCalled();
  });

  it("reports failures from transport actions", async () => {
    const onError = vi.fn();
    const a = actions({
      requestSnapshot: async () => {
        throw new Error("rate limited");
      }
    });
    expect(await recoverSession(a, { onError })).toBe("failed");
    expect(onError).toHaveBeenCalledWith("rate limited");
  });
});

describe("BoundedBackoff", () => {
  it("grows to the cap and resets", () => {
    const backoff = new BoundedBackoff([100, 200], () => 0.5);
    expect(backoff.next()).toBe(100);
    expect(backoff.next()).toBe(200);
    expect(backoff.next()).toBe(200);
    expect(backoff.attempts).toBe(3);
    backoff.reset();
    expect(backoff.attempts).toBe(0);
    expect(backoff.next()).toBe(100);
  });

  it("applies bounded jitter", () => {
    const low = new BoundedBackoff([1_000], () => 0);
    const high = new BoundedBackoff([1_000], () => 1);
    expect(low.next()).toBe(800);
    expect(high.next()).toBe(1_200);
  });
});
