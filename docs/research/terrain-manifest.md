# Terrain manifest — autotile block coverage

Generated 2026-11-27 by `tools/scan_terrain_blocks.py` from `autotiles_32x32.png` (384×3328 px, 12×104 tiles of 32px).

**Why:** wiring a Godot terrain from a pack sheet is only safe when the chosen block supplies every
pattern the terrain mode needs. An incomplete block does not fail loudly — Godot falls back to a
wrong tile and the result merely looks *almost* right. Measure completeness; do not assume it.

## Summary

| Block | Tile rows | Tiles | Kind | Side patterns | Corner patterns | Usable as sides terrain |
|---:|---|---:|---|---:|---:|:---|
| 0 | 0–3 | 44 | opaque | 1/16 | 1/16 | no |
| 1 | 4–7 | 44 | opaque | 1/16 | 1/16 | no |
| 2 | 8–11 | 44 | opaque | 1/16 | 1/16 | no |
| 3 | 12–15 | 44 | opaque | 1/16 | 1/16 | no |
| 4 | 16–19 | 38 | transparent-set | 10/16 | 11/16 | no — 10/16 |
| 5 | 20–23 | 38 | transparent-set | 10/16 | 11/16 | no — 10/16 |
| 6 | 24–27 | 44 | opaque | 1/16 | 1/16 | no |
| 7 | 28–31 | 44 | opaque | 1/16 | 1/16 | no |
| 8 | 32–35 | 44 | opaque | 1/16 | 1/16 | no |
| 9 | 36–39 | 44 | transparent-set | 14/16 | 15/16 | no — 14/16 |
| 10 | 40–43 | 44 | opaque | 1/16 | 1/16 | no |
| 11 | 44–47 | 44 | opaque | 1/16 | 1/16 | no |
| 12 | 48–51 | 44 | transparent-set | 16/16 | 6/16 | **yes — complete (16/16)** |
| 13 | 52–55 | 44 | opaque | 1/16 | 1/16 | no |
| 14 | 56–59 | 44 | opaque | 1/16 | 1/16 | no |
| 15 | 60–63 | 44 | opaque | 1/16 | 1/16 | no |
| 16 | 64–67 | 39 | transparent-set | 12/16 | 11/16 | no — 12/16 |
| 17 | 68–71 | 44 | opaque | 1/16 | 1/16 | no |
| 18 | 72–75 | 44 | opaque | 1/16 | 1/16 | no |
| 19 | 76–79 | 44 | opaque | 1/16 | 1/16 | no |
| 20 | 80–83 | 44 | opaque | 1/16 | 1/16 | no |
| 21 | 84–87 | 44 | opaque | 1/16 | 1/16 | no |
| 22 | 88–91 | 44 | opaque | 1/16 | 1/16 | no |
| 23 | 92–95 | 44 | opaque | 1/16 | 1/16 | no |
| 24 | 96–99 | 44 | opaque | 1/16 | 1/16 | no |
| 25 | 100–103 | 44 | opaque | 1/16 | 1/16 | no |

## Findings

- Blocks with a **complete 16/16 side set** (safe to wire as a `TerrainMode.Sides` terrain): #12.
- **21 of 26 blocks are `kind: opaque`** and therefore *not analyzable by alpha at all* — not necessarily incomplete, just invisible to this method. Their terrain shape is defined by
  colour (e.g. water vs grass), so establishing those blocks' coverage needs a colour/semantic pass.
  Treat `no` in the last column as *unknown* for those rows, never as proof of incompleteness.
- An `opaque` block must never be scored by alpha: every tile looks identical to the detector, so a
  "13/13 roles" reading there is an artifact of the measurement, not coverage.
- `kind: uniform` blocks supply a single pattern only — decorative sheets, not autotile sources.
- `corner_patterns` is recorded for reference; a corner-mode terrain additionally needs all four
  inner-corner patterns. These blocks do not provide them, which is why sides mode is the right mode here.

## Worked example

Block #12 (rows 48–51) is the complete one. Wired from its 16 `side_pattern_tiles` into a
`TerrainMode.Sides` terrain and painted, it renders a continuous fringe with resolved corners and no
fallback tiles — see `terrain-manifest-block12.png`. Blocks #2/#4/#9 are the cautionary cases: wired as if
complete, they render with hard seams, because the missing patterns force Godot to substitute.

## How to use a complete block

1. Read `blocks[i].side_pattern_tiles` — one representative tile per pattern, as `[col,row]`.
2. Create a `TileSet` (tile size from `source.tile`), add a `TileSetAtlasSource` for the sheet.
3. For each pattern, `CreateTile` the coordinate, then set `TileData.TerrainSet/.Terrain` and call
   `SetTerrainPeeringBit` for each side marked `W` in the pattern string.
4. Paint with `SetCellsTerrainConnect`. Read cells back and render before shipping.

Set `texture_filter = Nearest` for pixel art. The sheet's column 0 is a transparent spacer.
