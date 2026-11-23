# World Rendering — Summary

**Folder:** `02-world-rendering/`
**Status:** Planned (V3)

**Depends on:** client foundation (`01/tech/01–05`), `MapSnapshot`, `MapTileRun`, `MapBuildingStatus`
**Feeds:** realtime presence, input/targeting and the playable vertical slice

## Objective

Render the deterministic world from `MapSnapshot` before live replication is involved: decode the RLE layout, build a Pixi tile and building scene, add camera controls, cull off-screen work and expose a stable render loop that the network layer can feed.

## Baseline before V3

- `MapSnapshot` carries metadata, RLE tiles and a separate building list.
- The default world is 256×256 tiles at 32 px, with signed bounds centered on the origin.
- Tile flags are `Land`, `Road`, `Water`, `Occupied` and `Reserved`.
- Building occupancy is authoritative in `MapSnapshot.Buildings`.
- There is no Pixi scene, tileset, atlas or camera.
- The admin map page is SVG and unrelated to the game client.

## Deliverables

1. A Pixi application with explicit scene layers and a render ticker.
2. A map snapshot cache keyed by `MapId` + `Version`.
3. A tested RLE decoder that produces a dense tile index and validates coverage.
4. A tile/road renderer using an atlas and batched sprites or Pixi tilemap.
5. Building footprints, entry markers and selection-ready hit areas.
6. Camera pan/zoom, viewport calculation, culling and debug overlays.
7. A fixture-driven rendering acceptance path with no server dependency.

## Architecture

```text
MapSnapshot
   │
   ├── metadata ──► MapCache (MapId + Version)
   ├── Tiles[] ───► RleDecoder ──► TileIndex
   └── Buildings[] ► BuildingIndex
                              │
                              ▼
                    Pixi scene layers
        terrain → roads → buildings → sims → selection → effects
                              │
                         Camera2D + culling
```

The renderer owns visual state. It does not own authority, snapshots or connection state.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Pixi scene and render loop](tech/01-pixi-scene-and-render-loop.md) | Rendering |
| 02 | [Map snapshot cache and RLE decoder](tech/02-map-snapshot-cache-and-rle-decoder.md) | Map |
| 03 | [Tiles, buildings and layers](tech/03-tiles-buildings-and-layers.md) | Rendering |
| 04 | [Camera, culling and overlays](tech/04-camera-culling-and-overlays.md) | View |
| 05 | [Rendering acceptance](tech/05-rendering-acceptance.md) | Verification |

## Business reading

- [Map as the stage](business/01-map-as-the-stage.md)
- [Visual language and scale](business/02-visual-language-and-scale.md)

## Definition of done

- The full map renders from a recorded `MapSnapshot` fixture.
- RLE coverage exactly equals `Width × Height`; malformed runs are rejected.
- Roads, land, water and building footprints are visually distinct.
- Buildings render from the building list, not from tile occupancy hints.
- The camera pans and zooms within bounds; off-screen layers are culled.
- A debug overlay shows tile coordinates, map version, camera bounds and visible counts.
- The scene remains interactive at 2,000 simulated sims when fed a fixture snapshot.
