import { describe, expect, it } from "vitest";
import {
  computeRenderTick,
  interpolateFrames,
  pickFrames,
  type SimFrame
} from "./interpolation";

function frame(
  tick: number,
  arrivedAt: number,
  positions: Array<[number, number, number, number, number]>
): SimFrame {
  return {
    tick,
    arrivedAt,
    positions: new Map(
      positions.map(([id, x, y, tileX, tileY]) => [id, { x, y, tileX, tileY }])
    )
  };
}

const options = { snapDistance: 3, maxFrameGapTicks: 3 };

describe("computeRenderTick", () => {
  it("returns the newest tick in reduced motion", () => {
    const frames = [frame(10, 0, []), frame(11, 40, [])];
    expect(
      computeRenderTick(frames, 500, {
        tickIntervalMs: 40,
        interpolationDelayMs: 120,
        reducedMotion: true
      })
    ).toBe(11);
  });

  it("rewinds by the interpolation delay and never extrapolates", () => {
    const frames = [frame(10, 0, []), frame(11, 40, [])];
    expect(
      computeRenderTick(frames, 40, { tickIntervalMs: 40, interpolationDelayMs: 0 })
    ).toBe(11);
    expect(
      computeRenderTick(frames, 40, { tickIntervalMs: 40, interpolationDelayMs: 40 })
    ).toBe(10);
    expect(
      computeRenderTick(frames, 10_000, { tickIntervalMs: 40, interpolationDelayMs: 0 })
    ).toBe(11);
  });

  it("clamps to the oldest retained tick", () => {
    const frames = [frame(10, 0, []), frame(11, 40, [])];
    expect(
      computeRenderTick(frames, 40, { tickIntervalMs: 40, interpolationDelayMs: 400 })
    ).toBe(10);
  });

  it("returns null without frames", () => {
    expect(
      computeRenderTick([], 0, { tickIntervalMs: 40, interpolationDelayMs: 0 })
    ).toBeNull();
  });
});

describe("pickFrames", () => {
  it("finds the bracketing pair and alpha", () => {
    const frames = [frame(1, 0, []), frame(2, 40, []), frame(3, 80, [])];
    const pair = pickFrames(frames, 2.5);
    expect(pair?.lower.tick).toBe(2);
    expect(pair?.upper.tick).toBe(3);
    expect(pair?.alpha).toBeCloseTo(0.5, 5);
  });

  it("handles a single frame", () => {
    const frames = [frame(5, 0, [])];
    const pair = pickFrames(frames, 5);
    expect(pair?.alpha).toBe(0);
    expect(pair?.lower.tick).toBe(5);
  });
});

describe("interpolateFrames", () => {
  it("lerps positions while keeping the newer tile", () => {
    const pair = pickFrames(
      [
        frame(1, 0, [[1, 0, 0, 0, 0]]),
        frame(2, 40, [[1, 1, 2, 1, 2]])
      ],
      1.5
    );
    if (pair === null) throw new Error("expected frames");
    const sims = interpolateFrames(pair, options);
    expect(sims.get(1)?.x).toBeCloseTo(0.5, 5);
    expect(sims.get(1)?.y).toBeCloseTo(1, 5);
    expect(sims.get(1)?.tileX).toBe(1);
    expect(sims.get(1)?.interpolated).toBe(true);
  });

  it("drops removed sims and snaps new sims", () => {
    const pair = pickFrames(
      [
        frame(1, 0, [[1, 0, 0, 0, 0], [2, 2, 2, 2, 2]]),
        frame(2, 40, [[1, 0, 0, 0, 0], [3, 5, 5, 5, 5]])
      ],
      1.5
    );
    if (pair === null) throw new Error("expected frames");
    const sims = interpolateFrames(pair, options);
    expect(sims.has(2)).toBe(false);
    expect(sims.get(3)?.snapped).toBe(true);
    expect(sims.get(3)?.x).toBe(5);
  });

  it("snaps on distance and frame gaps", () => {
    const far = pickFrames(
      [
        frame(1, 0, [[1, 0, 0, 0, 0]]),
        frame(2, 40, [[1, 10, 0, 10, 0]])
      ],
      1.5
    );
    if (far === null) throw new Error("expected frames");
    expect(interpolateFrames(far, options).get(1)?.snapped).toBe(true);

    const sparse = pickFrames(
      [
        frame(1, 0, [[1, 0, 0, 0, 0]]),
        frame(9, 320, [[1, 8, 0, 8, 0]])
      ],
      5
    );
    if (sparse === null) throw new Error("expected frames");
    expect(interpolateFrames(sparse, options).get(1)?.snapped).toBe(true);
  });
});
