import { describe, expect, it } from "vitest";
import { LatencyRecorder, percentile } from "./latency";

describe("latency recorder", () => {
  it("computes p50 and p95 from admission round trips", () => {
    const recorder = new LatencyRecorder();
    for (const value of [10, 20, 30, 40, 100]) recorder.record(1_000, 1_000 + value);

    expect(recorder.stats()).toEqual({ count: 5, p50Ms: 30, p95Ms: 100, maxMs: 100 });
  });

  it("bounds the sample window and ignores invalid samples", () => {
    const recorder = new LatencyRecorder(3);
    for (const value of [5, 10, 15, 20]) recorder.record(0, value);
    recorder.record(10, 5);

    expect(recorder.size).toBe(3);
    expect(recorder.stats().maxMs).toBe(20);
  });

  it("returns a zero report before any sample", () => {
    expect(new LatencyRecorder().stats()).toEqual({ count: 0, p50Ms: 0, p95Ms: 0, maxMs: 0 });
  });

  it("interpolates percentiles at the array boundary", () => {
    expect(percentile([], 0.95)).toBe(0);
    expect(percentile([1], 0.95)).toBe(1);
    expect(percentile([1, 2, 3, 4], 0.5)).toBe(2);
    expect(percentile([1, 2, 3, 4], 0.95)).toBe(4);
  });
});
