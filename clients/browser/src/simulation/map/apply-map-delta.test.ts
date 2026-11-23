import { describe, expect, it } from "vitest";
import { applyMapDelta } from "./apply-map-delta";
import { decodeMap } from "./rle-decoder";
import { validateMapSnapshot } from "../../contracts/validate";
import mapFixture from "../../test/fixtures/map-snapshot.json";
import deltaFixture from "../../test/fixtures/map-delta.json";

const map = decodeMap(validateMapSnapshot(mapFixture));

describe("applyMapDelta", () => {
  it("applies additions, updates and removals", () => {
    const outcome = applyMapDelta(map, deltaFixture);
    expect(outcome.status).toBe("applied");
    if (outcome.status !== "applied") return;

    expect(outcome.map.version).toBe(4);
    expect(outcome.map.buildings.has(7004)).toBe(true);
    expect(outcome.map.buildings.get(7002)?.name).toBe("Foundry Office (rebuilt)");
    expect(outcome.map.buildings.has(7003)).toBe(false);
    // Static terrain and bounds are preserved.
    expect(outcome.map.flags).toBe(map.flags);
    expect(outcome.map.minX).toBe(map.minX);
  });

  it("reports a version gap instead of applying", () => {
    const outcome = applyMapDelta(map, { ...deltaFixture, fromVersion: 2 });
    expect(outcome.status).toBe("gap");
  });

  it("reports a map id mismatch", () => {
    const outcome = applyMapDelta(map, { ...deltaFixture, mapId: "other" });
    expect(outcome.status).toBe("mismatch");
  });

  it("rejects backwards versions", () => {
    const outcome = applyMapDelta(map, { ...deltaFixture, toVersion: 1 });
    expect(outcome.status).toBe("gap");
  });
});
