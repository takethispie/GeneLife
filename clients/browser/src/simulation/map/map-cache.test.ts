import { describe, expect, it } from "vitest";
import { validateMapDelta, validateMapSnapshot } from "../../contracts/validate";
import mapFixture from "../../test/fixtures/map-snapshot.json";
import mapDeltaFixture from "../../test/fixtures/map-delta.json";
import { MapCache } from "./map-cache";

describe("MapCache", () => {
  it("tracks map identity and version", () => {
    const cache = new MapCache();
    expect(cache.current).toBeNull();
    const map = cache.setSnapshot(validateMapSnapshot(mapFixture));
    expect(cache.key).toBe(`world:${map.version}`);
    expect(cache.matches("world", map.version)).toBe(true);
    expect(cache.matches("other", map.version)).toBe(false);
  });

  it("advances on deltas and marks gaps stale", () => {
    const cache = new MapCache();
    cache.setSnapshot(validateMapSnapshot(mapFixture));
    const applied = cache.applyDelta(validateMapDelta(mapDeltaFixture));
    expect(applied.status).toBe("applied");
    expect(cache.current?.version).toBe(4);
    expect(cache.isStale).toBe(false);

    const gap = cache.applyDelta({ ...validateMapDelta(mapDeltaFixture), fromVersion: 1 });
    expect(gap.status).toBe("gap");
    expect(cache.isStale).toBe(true);
  });

  it("reports a gap without any cached map", () => {
    const cache = new MapCache();
    expect(cache.applyDelta(validateMapDelta(mapDeltaFixture)).status).toBe("gap");
  });
});
