# R02 — Map snapshot cache and RLE decoder

**Phase:** 2 — Map
**Depends:** R01, F02
**Estimate:** 1–2 sessions
**Status:** Planned (V3)

## Goal

Decode `MapSnapshot` into a dense tile index once, validate it, and cache it by `MapId` + `Version` so map deltas can be applied without re-decoding the world.

## Context

`MapSnapshot.Tiles` is run-length encoded per row:

```json
{ "x": -128, "y": -128, "length": 12, "flags": "Land, Road" }
```

The default map is 256×256 at 32 px. Rendering 65,536 individual DOM or Pixi objects is not acceptable.

## Design

```ts
type TileFlag =
  | "Land"
  | "Road"
  | "Water"
  | "Occupied"
  | "Reserved";

type DecodedMap = {
  mapId: string;
  version: number;
  width: number;
  height: number;
  tileSize: number;
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
  flags: Uint8Array; // width * height
  buildings: Map<number, BuildingView>;
};
```

Decode algorithm:

1. Validate metadata: positive width/height/tile size and `max >= min`.
2. Allocate `flags` once.
3. For each run, validate `x`, `y`, `length` inside bounds and non-overlapping.
4. Write the parsed flag bitset into every cell.
5. Assert coverage equals `width * height`; fail closed on gaps or overlaps.
6. Index buildings by `NetworkId` separately.

Cache:

- Key: `${mapId}:${version}`.
- `SendMapSnapshot` replaces the cache.
- `SendMapDelta` advances the version when `fromVersion` matches the cache; a gap requests a fresh snapshot.
- Never infer building occupancy from `Occupied` flags.

## Tasks

- [ ] Implement the RLE decoder and flag parser.
- [ ] Validate coverage, overlap and bounds.
- [ ] Add the map cache and version rules.
- [ ] Apply map deltas or force refresh on a gap.
- [ ] Add fixture tests for valid and malformed runs.
- [ ] Add a map-version metric for the debug overlay.

## Files

- new `clients/browser/src/simulation/map/rle-decoder.ts`
- new `clients/browser/src/simulation/map/map-cache.ts`
- new `clients/browser/src/simulation/map/decode-map.test.ts`
- `clients/browser/src/test/fixtures/map-snapshot.json`

## Acceptance

- A valid fixture decodes to exactly `width * height` tiles.
- A gap, overlap, out-of-bounds run or zero/negative length fails.
- The cache is reused across renders and replaced on a new map version.
- A map delta with the expected `fromVersion` updates the cache.
- A version gap requests `RequestMapSnapshot`.

## Testing

- Fixture decode tests.
- Malformed-run tests.
- Cache hit/replacement tests.
- Map delta/version-gap tests.
