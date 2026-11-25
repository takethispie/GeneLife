import { describe, expect, it } from "vitest";
import {
  validateMapDelta,
  validateMapSnapshot,
  validateWorldSnapshot,
  validateWorldSnapshotDelta
} from "../contracts/validate";
import mapFixture from "./fixtures/map-snapshot.json";
import mapDeltaFixture from "./fixtures/map-delta.json";
import worldFixture from "./fixtures/world-snapshot.json";
import worldDeltaFixture from "./fixtures/world-delta.json";
import { SimulationStore } from "../simulation/store";

describe("end-to-end client scenarios", () => {
  it("applies the initial map and world, then live deltas", () => {
    const store = new SimulationStore();
    const map = validateMapSnapshot(mapFixture);
    const world = validateWorldSnapshot(worldFixture);
    const delta = validateWorldSnapshotDelta(worldDeltaFixture);

    store.applyMapSnapshot(map);
    store.setOwnGenos(1);
    expect(store.applySnapshot(world, 0).status).toBe("applied");

    viewport(store);
    expect(store.applyDelta(delta, 40).status).toBe("applied");
    viewport(store);

    const state = store.getRenderState(80, {
      tickIntervalMs: 40,
      interpolationDelayMs: 40
    });
    expect(state).not.toBeNull();
    expect(state?.tick).toBe(101);
    expect(state?.genoses.has(store.ownGenosId)).toBe(true);
    expect(state?.genoses.size).toBe(4);
  });

  it("recovers with a full snapshot after a delta gap", () => {
    const store = new SimulationStore();
    store.applyMapSnapshot(validateMapSnapshot(mapFixture));
    store.applySnapshot(validateWorldSnapshot(worldFixture), 0);

    const gap = store.applyDelta(
      { ...validateWorldSnapshotDelta(worldDeltaFixture), sinceTick: 12 },
      40
    );
    expect(gap.status).toBe("gap");
    expect(gap.requestFullSnapshot).toBe(true);

    const full = store.applySnapshot(validateWorldSnapshot(worldFixture), 80);
    expect(full.status).toBe("applied");
    expect(store.tick).toBe(100);
  });

  it("applies a building delta and exposes the new layout", () => {
    const store = new SimulationStore();
    const map = validateMapSnapshot(mapFixture);
    store.applyMapSnapshot(map);
    store.applySnapshot(validateWorldSnapshot(worldFixture), 0);

    const outcome = store.applyMapDelta(validateMapDelta(mapDeltaFixture));
    expect(outcome.status).toBe("applied");
    expect(store.map?.buildings.get(7004)?.name).toBe("Corner Shop");
    expect(store.map?.buildings.has(7003)).toBe(false);
  });

  it("requests a map refresh instead of rendering mismatched state", () => {
    const store = new SimulationStore();
    store.applyMapSnapshot(validateMapSnapshot(mapFixture));
    store.applySnapshot(validateWorldSnapshot(worldFixture), 0);
    const gap = store.applyMapDelta({
      ...validateMapDelta(mapDeltaFixture),
      fromVersion: 1
    });
    expect(gap.status).toBe("gap");
    expect(store.isMapStale).toBe(true);
    const snapshot = store.applySnapshot(validateWorldSnapshot(worldFixture), 40);
    expect(snapshot.status).toBe("map-stale");
    expect(snapshot.requestMapSnapshot).toBe(true);
  });

  it("fails closed on malformed contract payloads", () => {
    expect(() => validateWorldSnapshot({ tick: "soon" })).toThrowError(/tick/);
    expect(() => validateMapSnapshot({ ...mapFixture, tileSize: 0 })).toThrowError(
      /tileSize must be positive/
    );
  });
});

function viewport(store: SimulationStore): void {
  const map = store.map;
  const genoses = Array.from(store.genoses.values());
  expect(map).not.toBeNull();
  expect(genoses.every((genos) => genos.tileX >= (map?.minX ?? 0) && genos.tileX <= (map?.maxX ?? 0))).toBe(
    true
  );
}
