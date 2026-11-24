import { describe, expect, it } from "vitest";
import { validateMapSnapshot } from "../contracts/validate";
import { TileVisualCache } from "../render/map/tile-visual-cache";
import { decodeMap as decode } from "../simulation/map/rle-decoder";
import { interpolateFrames, pickFrames, type SimFrame } from "../simulation/interpolation";
import largeMapFixture from "./fixtures/map-snapshot-large.json";

describe("client performance budgets", () => {
  it("interpolates 2,000 sims inside the frame budget", () => {
    const older = new Map<number, { x: number; y: number; tileX: number; tileY: number }>();
    const newer = new Map<number, { x: number; y: number; tileX: number; tileY: number }>();
    for (let i = 0; i < 2_000; i++) {
      older.set(i, { x: i % 100, y: Math.floor(i / 100), tileX: i % 100, tileY: Math.floor(i / 100) });
      newer.set(i, {
        x: (i % 100) + 0.5,
        y: Math.floor(i / 100),
        tileX: i % 100,
        tileY: Math.floor(i / 100)
      });
    }

    const frames: SimFrame[] = [
      { tick: 1, arrivedAt: 0, positions: older },
      { tick: 2, arrivedAt: 40, positions: newer }
    ];
    const pair = pickFrames(frames, 1.5);
    if (pair === null) throw new Error("expected frames");

    const started = performance.now();
    const result = interpolateFrames(pair, { snapDistance: 3, maxFrameGapTicks: 3 });
    const elapsed = performance.now() - started;

    expect(result.size).toBe(2_000);
    expect(elapsed).toBeLessThan(16.7);
  });

  it("builds the 256x256 tile visual cache once within the startup budget", () => {
    const map = decode(validateMapSnapshot(largeMapFixture));
    const cache = new TileVisualCache();
    const started = performance.now();
    cache.ensure(map);
    const elapsed = performance.now() - started;

    expect(cache.terrain).toHaveLength(65_536);
    expect(elapsed).toBeLessThan(250);
  });

  it("reads the whole cached tile map inside a frame budget", () => {
    const map = decode(validateMapSnapshot(largeMapFixture));
    const cache = new TileVisualCache();
    cache.ensure(map);

    // Best of three: CI runs this with parallel builds and the first pass pays
    // JIT/allocator warm-up. The renderer only reads the visible viewport.
    let checksum = 0;
    let best = Number.POSITIVE_INFINITY;
    for (let attempt = 0; attempt < 3; attempt++) {
      const started = performance.now();
      checksum = 0;
      for (let index = 0; index < cache.terrain.length; index++) {
        checksum = (checksum + (cache.terrain[index] ?? 0) + (cache.road[index] ?? 0)) | 0;
      }
      best = Math.min(best, performance.now() - started);
    }

    expect(checksum).not.toBe(0);
    expect(best).toBeLessThan(16.7);
  });

  it("decodes the 256x256 fixture within the startup budget", () => {
    const started = performance.now();
    const map = decode(validateMapSnapshot(largeMapFixture));
    const elapsed = performance.now() - started;
    expect(map.flags).toHaveLength(65_536);
    expect(elapsed).toBeLessThan(150);
  });
});
