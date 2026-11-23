import { describe, expect, it } from "vitest";
import type { MapSnapshot } from "../../contracts/hub";
import { validateMapSnapshot } from "../../contracts/validate";
import smallFixture from "../../test/fixtures/map-snapshot.json";
import largeFixture from "../../test/fixtures/map-snapshot-large.json";
import {
  buildingAt,
  decodeMap,
  flagsAt,
  hasFlag,
  inBounds,
  isWalkableHint,
  parseTileFlags,
  tileIndex
} from "./rle-decoder";

const small = validateMapSnapshot(smallFixture) as MapSnapshot;

function snapshotWithTiles(tiles: MapSnapshot["tiles"]): MapSnapshot {
  return { ...small, tiles };
}

describe("parseTileFlags", () => {
  it("parses single, combined and empty flags", () => {
    expect(parseTileFlags("Land")).toBe(1);
    expect(parseTileFlags("Land, Road")).toBe(3);
    expect(parseTileFlags("Water")).toBe(16);
    expect(parseTileFlags("None")).toBe(0);
    expect(parseTileFlags("")).toBe(0);
  });

  it("rejects unknown flags", () => {
    expect(() => parseTileFlags("Land, Lava")).toThrowError(/unknown tile flag "Lava"/);
  });
});

describe("decodeMap", () => {
  it("decodes the fixture into a dense flag array", () => {
    const map = decodeMap(small);
    expect(map.flags).toHaveLength(32 * 32);
    expect(map.buildings.size).toBe(3);
    expect(inBounds(map, map.minX, map.minY)).toBe(true);
    expect(inBounds(map, map.maxX + 1, map.maxY)).toBe(false);
    expect(tileIndex(map, map.minX, map.minY)).toBe(0);

    const water = flagsAt(map, -15, -15);
    expect(hasFlag(water, "Water")).toBe(true);
    expect(isWalkableHint(map, -15, -15)).toBe(false);

    const road = flagsAt(map, 0, 0);
    expect(hasFlag(road, "Road")).toBe(true);
    expect(isWalkableHint(map, 0, 0)).toBe(true);
  });

  it("finds buildings by footprint and not by entry", () => {
    const map = decodeMap(small);
    expect(buildingAt(map, -6, -6)?.networkId).toBe(7001);
    expect(buildingAt(map, -5, -4)).toBeNull();
  });

  it("decodes the 256x256 fixture quickly", () => {
    const started = performance.now();
    const map = decodeMap(validateMapSnapshot(largeFixture));
    const elapsed = performance.now() - started;
    expect(map.flags).toHaveLength(256 * 256);
    expect(elapsed).toBeLessThan(150);
  });

  it("rejects gaps in RLE coverage", () => {
    const broken = snapshotWithTiles(small.tiles.slice(1));
    expect(() => decodeMap(broken)).toThrowError(/does not equal width \* height/);
  });

  it("rejects overlapping runs", () => {
    const broken = snapshotWithTiles([
      ...small.tiles,
      { x: small.minX, y: small.minY, length: 1, flags: "Land" }
    ]);
    expect(() => decodeMap(broken)).toThrowError(/overlapping runs/);
  });

  it("rejects runs that extend past the map", () => {
    const broken = snapshotWithTiles([{ x: small.maxX, y: small.minY, length: 2, flags: "Land" }]);
    expect(() => decodeMap(broken)).toThrowError(/past the right map edge/);
  });

  it("rejects zero-length runs", () => {
    const broken = snapshotWithTiles([{ x: small.minX, y: small.minY, length: 0, flags: "Land" }]);
    expect(() => decodeMap(broken)).toThrowError(/run length must be a positive integer/);
  });

  it("rejects duplicate building ids", () => {
    const broken = { ...small, buildings: [small.buildings[0]!, small.buildings[0]!] };
    expect(() => decodeMap(broken)).toThrowError(/duplicate building network id/);
  });
});
