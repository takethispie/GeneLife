# R03 — Tiles, buildings and layers

**Phase:** 3 — Rendering
**Depends:** R02
**Estimate:** 2 sessions
**Status:** Planned (V3)

## Goal

Turn the decoded map into a readable scene: terrain, roads, water, building footprints and entry markers, using an atlas and batched rendering rather than one object per tile.

## Context

The server’s tile flags are presentation hints. Buildings come from `MapSnapshot.Buildings` and remain authoritative. The first art set can be simple; correctness and layer order matter more than detail.

## Design

### Terrain and roads

- Use a Pixi tilemap or batched sprites backed by one atlas.
- Terrain variants are chosen by a deterministic hash of tile coordinates plus flags.
- Roads connect by a bitmask of neighbours; compute once when the map decodes.
- Water is a distinct tile family, not a tint of land.

### Buildings

```ts
type BuildingView = {
  networkId: number;
  kind: string;
  name: string;
  x: number;
  y: number;
  width: number;
  height: number;
  entryX: number;
  entryY: number;
};
```

- Render the footprint in the buildings layer.
- Render the entry as a separate marker in the selection/effects layer.
- Use a silhouette per kind and tint by district/type.
- Building hit areas are derived from the footprint, not from tile flags.

### Layer rules

```text
terrain → roads → buildings → sims → selection → effects
```

- Labels are optional and only at close zoom.
- Selection and target markers always render above sims.
- Building occupancy comes from the building list; `Occupied` flags are informational.

## Tasks

- [ ] Build the tileset atlas and tile selection rules.
- [ ] Render terrain, water and roads from decoded flags.
- [ ] Render building footprints and entries.
- [ ] Add building hit areas for selection.
- [ ] Add labels/nameplates behind a zoom threshold.
- [ ] Add an atlas fallback for missing textures.

## Files

- new `clients/browser/src/render/map/tile-layer.ts`
- new `clients/browser/src/render/map/building-layer.ts`
- new `clients/browser/src/render/map/tileset.ts`
- `clients/browser/public/atlases/*`

## Acceptance

- Terrain, water and roads are visually distinct without labels.
- Buildings render at the correct footprint and entry.
- No tile-per-object explosion; the tile layer is batched.
- A missing texture produces a visible fallback, not a crash.
- Building selection does not depend on tile flags.

## Testing

- Road-neighbour bitmask tests.
- Building footprint/entry mapping tests.
- Missing-atlas fallback test.
- Snapshot visual check against a fixture.
