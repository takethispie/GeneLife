import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { ViewportResult } from "../contracts/hub";
import {
  visibleRectToViewport,
  ViewportSynchronizer
} from "./viewport-client";

describe("visibleRectToViewport", () => {
  it("computes a stable center and radius", () => {
    expect(visibleRectToViewport({ minX: -10, minY: -4, maxX: 10, maxY: 4 })).toEqual({
      centerX: 0,
      centerY: 0,
      radius: 11
    });
  });

  it("rounds odd extents upward", () => {
    expect(visibleRectToViewport({ minX: 0, minY: 0, maxX: 7, maxY: 3 })).toEqual({
      centerX: 4,
      centerY: 2,
      radius: 4
    });
  });
});

describe("ViewportSynchronizer", () => {
  beforeEach(() => {
    vi.useFakeTimers();
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  function success(request: { centerX: number; centerY: number; radius: number }): ViewportResult {
    return { success: true, ...request };
  }

  it("debounces multiple updates into one request", async () => {
    const setViewport = vi.fn(async (request) => success(request));
    const sync = new ViewportSynchronizer({ setViewport, debounceMs: 200 });

    sync.update({ centerX: 1, centerY: 1, radius: 8 });
    sync.update({ centerX: 2, centerY: 2, radius: 8 });
    sync.update({ centerX: 3, centerY: 3, radius: 8 });
    expect(setViewport).not.toHaveBeenCalled();

    await vi.advanceTimersByTimeAsync(200);
    expect(setViewport).toHaveBeenCalledTimes(1);
    expect(setViewport).toHaveBeenCalledWith({ centerX: 3, centerY: 3, radius: 8 });
    expect(sync.effectiveViewport).toEqual({ centerX: 3, centerY: 3, radius: 8 });
  });

  it("does not let identical per-frame updates starve the debounce", async () => {
    const setViewport = vi.fn(async (request) => success(request));
    const sync = new ViewportSynchronizer({ setViewport, debounceMs: 200 });

    const request = { centerX: 1, centerY: 2, radius: 8 };
    sync.update(request);
    await vi.advanceTimersByTimeAsync(100);
    sync.update(request);
    await vi.advanceTimersByTimeAsync(100);

    expect(setViewport).toHaveBeenCalledTimes(1);
  });

  it("skips sub-threshold changes", async () => {
    const setViewport = vi.fn(async (request) => success(request));
    const sync = new ViewportSynchronizer({
      setViewport,
      debounceMs: 10,
      changeThreshold: 2
    });

    sync.update({ centerX: 0, centerY: 0, radius: 10 });
    await vi.advanceTimersByTimeAsync(10);
    sync.update({ centerX: 1, centerY: 1, radius: 11 });
    await vi.advanceTimersByTimeAsync(10);

    expect(setViewport).toHaveBeenCalledTimes(1);
  });

  it("stores the server's clamped viewport", async () => {
    const setViewport = vi.fn(async () => ({
      success: true,
      centerX: 0,
      centerY: 0,
      radius: 4
    }));
    const onResult = vi.fn();
    const sync = new ViewportSynchronizer({ setViewport, onResult, debounceMs: 10 });

    sync.update({ centerX: 50, centerY: 50, radius: 1 });
    await vi.advanceTimersByTimeAsync(10);

    expect(sync.effectiveViewport).toEqual({ centerX: 0, centerY: 0, radius: 4 });
    expect(sync.stats.clamped).toBe(1);
    expect(onResult).toHaveBeenCalledWith({ success: true, centerX: 0, centerY: 0, radius: 4 });
  });

  it("reports failures without storing a viewport", async () => {
    const onError = vi.fn();
    const setViewport = vi.fn(async () => ({
      success: false,
      centerX: 0,
      centerY: 0,
      radius: 0,
      error: "rate limited"
    }));
    const sync = new ViewportSynchronizer({ setViewport, onError, debounceMs: 10 });

    sync.update({ centerX: 1, centerY: 2, radius: 8 });
    await vi.advanceTimersByTimeAsync(10);

    expect(sync.effectiveViewport).toBeNull();
    expect(sync.stats.errors).toBe(1);
    expect(onError).toHaveBeenCalledWith("rate limited");
  });

  it("resyncs the last effective viewport", async () => {
    const setViewport = vi.fn(async (request) => success(request));
    const sync = new ViewportSynchronizer({ setViewport, debounceMs: 10 });
    sync.update({ centerX: 5, centerY: 5, radius: 8 });
    await vi.advanceTimersByTimeAsync(10);

    await sync.resync();
    expect(setViewport).toHaveBeenCalledTimes(2);
    expect(setViewport).toHaveBeenLastCalledWith({ centerX: 5, centerY: 5, radius: 8 });
  });

  it("does not overlap in-flight requests", async () => {
    const gate: { resolve: ((value: ViewportResult) => void) | null } = { resolve: null };
    const setViewport = vi.fn(
      (request: { centerX: number; centerY: number; radius: number }) =>
        new Promise<ViewportResult>((resolve) => {
          if (gate.resolve === null) gate.resolve = () => resolve(success(request));
          else resolve(success(request));
        })
    );
    const sync = new ViewportSynchronizer({ setViewport, debounceMs: 0 });

    sync.update({ centerX: 1, centerY: 1, radius: 8 });
    await vi.advanceTimersByTimeAsync(0);
    expect(setViewport).toHaveBeenCalledTimes(1);

    sync.update({ centerX: 2, centerY: 2, radius: 8 });
    await vi.advanceTimersByTimeAsync(0);
    expect(setViewport).toHaveBeenCalledTimes(1);

    gate.resolve?.({
      success: true,
      centerX: 1,
      centerY: 1,
      radius: 8
    });
    await vi.runAllTimersAsync();
    expect(setViewport).toHaveBeenCalledTimes(2);
    expect(setViewport).toHaveBeenLastCalledWith({ centerX: 2, centerY: 2, radius: 8 });
  });
});
