import { describe, expect, it } from "vitest";
import { TrafficMeter } from "./traffic";

describe("TrafficMeter", () => {
  it("reports bytes per second over a sliding window", () => {
    const meter = new TrafficMeter(1_000);
    meter.add(1_000, 0);
    meter.add(1_000, 500);
    expect(meter.bytesPerSecond(500)).toBe(2_000);
  });

  it("prunes old samples", () => {
    const meter = new TrafficMeter(1_000);
    meter.add(5_000, 0);
    expect(meter.bytesPerSecond(2_000)).toBe(0);
  });

  it("measures JSON payload sizes", () => {
    const meter = new TrafficMeter(1_000);
    meter.addPayload({ hello: "world" }, 0);
    expect(meter.bytesPerSecond(0)).toBeGreaterThan(0);
  });

  it("ignores invalid byte counts", () => {
    const meter = new TrafficMeter(1_000);
    meter.add(-10, 0);
    meter.add(Number.NaN, 0);
    expect(meter.bytesPerSecond(0)).toBe(0);
  });
});
