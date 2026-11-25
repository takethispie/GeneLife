import { describe, expect, it } from "vitest";
import { validateMapDelta, validateMapSnapshot, validateWorldSnapshot, validateWorldSnapshotDelta } from "../contracts/validate";
import mapFixture from "../test/fixtures/map-snapshot.json";
import mapDeltaFixture from "../test/fixtures/map-delta.json";
import worldFixture from "../test/fixtures/world-snapshot.json";
import worldDeltaFixture from "../test/fixtures/world-delta.json";
import world2000Fixture from "../test/fixtures/world-snapshot-2000.json";
import { SimulationStore } from "./store";

const map = validateMapSnapshot(mapFixture);
const world = validateWorldSnapshot(worldFixture);
const delta = validateWorldSnapshotDelta(worldDeltaFixture);

function storeWithMap(): SimulationStore {
  const store = new SimulationStore();
  store.applyMapSnapshot(map);
  return store;
}

describe("SimulationStore world replication", () => {
  it("replaces state atomically on a full snapshot", () => {
    const store = storeWithMap();
    const result = store.applySnapshot(world, 0);
    expect(result.status).toBe("applied");
    expect(store.tick).toBe(100);
    expect(store.genoses.size).toBe(5);
    expect(store.companies.size).toBe(1);
    expect(store.getStats().fullSnapshots).toBe(1);
  });

  it("applies deltas, removals and companies", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 0);
    const result = store.applyDelta(delta, 40);
    expect(result.status).toBe("applied");
    expect(store.tick).toBe(101);
    expect(store.genoses.has(5)).toBe(false);
    expect(store.genoses.get(1)?.x).toBe(0.5);
    expect(store.genoses.get(2)?.x).toBe(3.9);
    expect(store.getStats().appliedDeltas).toBe(1);
  });

  it("ignores a stale delta without rewinding", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 0);
    store.applyDelta(delta, 40);
    const result = store.applyDelta(delta, 80);
    expect(result.status).toBe("stale");
    expect(store.tick).toBe(101);
    expect(store.getStats().staleDeltas).toBe(1);
  });

  it("requests a full snapshot on a tick gap", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 0);
    const result = store.applyDelta({ ...delta, sinceTick: 99 }, 40);
    expect(result.status).toBe("gap");
    expect(result.requestFullSnapshot).toBe(true);
    expect(store.tick).toBe(100);
  });

  it("treats a full delta as a replacement snapshot", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 0);
    const result = store.applyDelta(
      { ...delta, full: true, sinceTick: 0, genoses: world.genoses, companies: world.companies, removedGenoses: [] },
      40
    );
    expect(result.status).toBe("applied");
    expect(store.genoses.size).toBe(5);
  });

  it("ignores an older full snapshot", () => {
    const store = storeWithMap();
    store.applySnapshot({ ...world, tick: 200 }, 0);
    const result = store.applySnapshot({ ...world, tick: 100 }, 40);
    expect(result.status).toBe("stale");
    expect(store.tick).toBe(200);
  });
});

describe("SimulationStore map coordination", () => {
  it("applies map deltas and tracks versions", () => {
    const store = storeWithMap();
    const outcome = store.applyMapDelta(validateMapDelta(mapDeltaFixture));
    expect(outcome.status).toBe("applied");
    expect(store.map?.version).toBe(4);
    expect(store.getStats().mapDeltas).toBe(1);
  });

  it("pauses world replication while the map is stale", () => {
    const store = storeWithMap();
    const gap = store.applyMapDelta({ ...validateMapDelta(mapDeltaFixture), fromVersion: 2 });
    expect(gap.status).toBe("gap");
    expect(store.isMapStale).toBe(true);

    const snapshotResult = store.applySnapshot(world, 0);
    expect(snapshotResult.status).toBe("map-stale");
    expect(snapshotResult.requestMapSnapshot).toBe(true);
    expect(store.genoses.size).toBe(0);

    store.applyMapSnapshot({ ...map, version: 4 });
    expect(store.isMapStale).toBe(false);
    expect(store.applySnapshot(world, 40).status).toBe("applied");
  });
});

describe("SimulationStore interpolation", () => {
  it("interpolates between consecutive frames", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 1000);
    store.applyDelta(delta, 1040);

    const state = store.getRenderState(1060, {
      tickIntervalMs: 40,
      interpolationDelayMs: 40,
      snapDistance: 3
    });

    expect(state).not.toBeNull();
    expect(state?.frameCount).toBe(2);
    // target tick 100.5: Ada moves 0 -> 0.5
    expect(state?.genoses.get(1)?.x).toBeCloseTo(0.25, 5);
    expect(state?.genoses.get(1)?.interpolated).toBe(true);
    expect(state?.genoses.has(5)).toBe(false);
  });

  it("never extrapolates past the newest tick", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 1000);
    store.applyDelta(delta, 1040);

    const state = store.getRenderState(10_000, {
      tickIntervalMs: 40,
      interpolationDelayMs: 0
    });
    expect(state?.targetTick).toBe(101);
    expect(state?.genoses.get(1)?.x).toBe(0.5);
  });

  it("returns latest positions in reduced-motion mode", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 1000);
    store.applyDelta(delta, 1040);
    const state = store.getRenderState(1060, {
      tickIntervalMs: 40,
      interpolationDelayMs: 120,
      reducedMotion: true
    });
    expect(state?.genoses.get(1)?.x).toBe(0.5);
    expect(state?.genoses.get(1)?.interpolated).toBe(false);
  });

  it("snaps instead of interpolating across a large correction", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 1000);
    store.applyDelta(
      {
        ...delta,
        genoses: [{ ...delta.genoses[0]!, x: 50, y: 0, tileX: 50 }]
      },
      1040
    );
    const state = store.getRenderState(1060, {
      tickIntervalMs: 40,
      interpolationDelayMs: 40,
      snapDistance: 3
    });
    expect(state?.genoses.get(1)?.snapped).toBe(true);
    expect(state?.genoses.get(1)?.x).toBe(50);
  });

  it("applies a 2000-genos snapshot within the frame budget", () => {
    const store = storeWithMap();
    const big = validateWorldSnapshot(world2000Fixture);
    const started = performance.now();
    const result = store.applySnapshot(big, 0);
    const elapsed = performance.now() - started;
    expect(result.status).toBe("applied");
    expect(store.genoses.size).toBe(2000);
    expect(elapsed).toBeLessThan(200);
  });
});

describe("SimulationStore ownership and reset", () => {
  it("tracks the player's own genos id", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 0);
    store.setOwnGenos(1);
    expect(store.ownGenosId).toBe(1);
    expect(store.positionOf(1)).toMatchObject({ tileX: 0, tileY: 0 });
    expect(store.positionOf(999)).toBeNull();
  });

  it("resets all state", () => {
    const store = storeWithMap();
    store.applySnapshot(world, 0);
    store.setOwnGenos(1);
    store.reset();
    expect(store.map).toBeNull();
    expect(store.tick).toBe(0);
    expect(store.genoses.size).toBe(0);
    expect(store.ownGenosId).toBe(0);
    expect(store.getStats().appliedTicks).toBe(0);
  });
});
