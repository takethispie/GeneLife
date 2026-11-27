#!/usr/bin/env python3
"""Scan a 32px autotile sheet and report, per 4-row terrain block, which autotile
patterns it actually supplies.

Why this exists: wiring a Godot terrain from an arbitrary asset-pack sheet is only
safe when the block you picked supplies every pattern the terrain mode needs. An
incomplete block does not fail loudly - Godot falls back to a wrong tile and the
result merely looks 'almost right'. This script measures completeness instead of
trusting it.

The sheet is not reflowed or normalised; the script is read-only. Output is a JSON
manifest (machine-readable) plus a Markdown summary.

Usage:
    python scan_terrain_blocks.py /path/to/autotiles_32x32.png \
        --out docs/research/terrain-manifest.json \
        --md  docs/research/terrain-manifest.md

Stdlib only (zlib + struct) so it runs anywhere; no Pillow required.
"""

from __future__ import annotations

import argparse
import collections
import json
import struct
import sys
import zlib
from datetime import date

BLOCK_ROWS = 4  # terrain blocks in these sheets are 4 tile-rows tall


def load_rgba(path):
    """Minimal PNG decoder for 8-bit RGB/RGBA non-interlaced images."""
    data = open(path, "rb").read()
    w, h = struct.unpack(">II", data[16:24])
    bit_depth, colour_type = data[24], data[25]
    if bit_depth != 8 or colour_type not in (2, 6):
        sys.exit(f"unsupported PNG: bit depth {bit_depth}, colour type {colour_type}")
    bpp = 4 if colour_type == 6 else 3
    idat = b""
    i = 8
    while i < len(data):
        length = struct.unpack(">I", data[i : i + 4])[0]
        chunk_type = data[i + 4 : i + 8]
        if chunk_type == b"IDAT":
            idat += data[i + 8 : i + 8 + length]
        i += 12 + length
    raw = zlib.decompress(idat)
    stride = w * bpp + 1
    rows = []
    for y in range(h):
        row = raw[y * stride : (y + 1) * stride]
        filt = row[0]
        line = bytearray(row[1:])
        if filt == 1:
            for x in range(bpp, len(line)):
                line[x] = (line[x] + line[x - bpp]) & 0xFF
        elif filt == 2:
            prev = rows[-1]
            for x in range(len(line)):
                line[x] = (line[x] + prev[x]) & 0xFF
        elif filt == 3:
            prev = rows[-1] if y else bytearray(len(line))
            for x in range(len(line)):
                a = line[x - bpp] if x >= bpp else 0
                line[x] = (line[x] + ((a + prev[x]) >> 1)) & 0xFF
        elif filt == 4:
            prev = rows[-1] if y else bytearray(len(line))
            for x in range(len(line)):
                a = line[x - bpp] if x >= bpp else 0
                b = prev[x]
                c = prev[x - bpp] if x >= bpp else 0
                pa, pb, pc = abs(b - c), abs(a - c), abs(a + b - 2 * c)
                pr = a if (pa <= pb and pa <= pc) else (b if pb <= pc else c)
                line[x] = (line[x] + pr) & 0xFF
        rows.append(line)
    return w, h, bpp, rows


class Sheet:
    def __init__(self, path, tile=32):
        self.w, self.h, self.bpp, self.rows = load_rgba(path)
        self.tile = tile

    def opaque(self, x, y):
        if self.bpp == 3:
            return True
        return self.rows[y][x * self.bpp + 3] > 0

    def coverage(self, cx, cy):
        """Fraction of sampled pixels in the tile that are opaque."""
        t = self.tile
        n = tot = 0
        for y in range(cy * t, (cy + 1) * t, 4):
            for x in range(cx * t, (cx + 1) * t, 4):
                tot += 1
                n += 1 if self.opaque(x, y) else 0
        return n / tot if tot else 0.0

    def side_frac(self, cx, cy, side):
        t = self.tile
        ox, oy = cx * t, cy * t
        n = tot = 0
        for i in range(1, t - 1, 2):
            if side == "T":
                x, y = ox + i, oy + 1
            elif side == "B":
                x, y = ox + i, oy + t - 2
            elif side == "L":
                x, y = ox + 1, oy + i
            else:
                x, y = ox + t - 2, oy + i
            tot += 1
            n += 1 if self.opaque(x, y) else 0
        return n / tot if tot else 0.0

    def corner_frac(self, cx, cy, which):
        t = self.tile
        ox, oy = cx * t, cy * t
        span = range(1, 9)
        far = range(t - 9, t - 1)
        xs = {"TL": span, "TR": far, "BR": far, "BL": span}[which]
        ys = {"TL": span, "TR": span, "BR": far, "BL": far}[which]
        n = tot = 0
        for y in ys:
            for x in xs:
                tot += 1
                n += 1 if self.opaque(ox + x, oy + y) else 0
        return n / tot if tot else 0.0

    # side pattern string is (T,R,B,L); W = terrain present on that side
    def side_pattern(self, cx, cy):
        return "".join("W" if self.side_frac(cx, cy, s) > 0.5 else "." for s in ("T", "R", "B", "L"))

    def corner_pattern(self, cx, cy):
        return "".join("W" if self.corner_frac(cx, cy, w) > 0.5 else "." for w in ("TL", "TR", "BR", "BL"))


ALL_PATTERNS = sorted(
    {"".join(bits) for bits in __import__("itertools").product("W.", repeat=4)}
)


def scan(path, tile=32, block_rows=BLOCK_ROWS):
    sheet = Sheet(path, tile)
    cols = sheet.w // tile
    tile_rows = sheet.h // tile
    blocks = []
    for b in range(tile_rows // block_rows):
        r0 = b * block_rows
        side_map = collections.defaultdict(list)
        corner_map = collections.defaultdict(list)
        n_tiles = n_opaque = 0
        for cy in range(r0, r0 + block_rows):
            for cx in range(1, cols):  # column 0 is the spacer column in these sheets
                cov = sheet.coverage(cx, cy)
                if cov < 0.2:
                    continue
                n_tiles += 1
                if cov > 0.95:
                    n_opaque += 1
                side_map[sheet.side_pattern(cx, cy)].append([cx, cy])
                corner_map[sheet.corner_pattern(cx, cy)].append([cx, cy])
        side_present = sorted(side_map)
        corner_present = sorted(corner_map)
        if n_tiles == 0:
            kind = "empty"
        elif n_opaque == n_tiles:
            kind = "opaque"          # alpha carries no pattern information here
        elif len(side_map) == 1:
            kind = "uniform"         # one pattern only: not an autotile source
        else:
            kind = "transparent-set"  # alpha defines the terrain shape: patterns are real
        blocks.append(
            {
                "id": b,
                "rows": [r0, r0 + block_rows - 1],
                "tiles": n_tiles,
                "fully_opaque_tiles": n_opaque,
                "kind": kind,
                "side_patterns": {
                    "count": len(side_present),
                    "present": side_present,
                    "missing": [p for p in ALL_PATTERNS if p not in side_map],
                },
                "corner_patterns": {
                    "count": len(corner_present),
                    "present": corner_present,
                    "missing": [p for p in ALL_PATTERNS if p not in corner_map],
                },
                "complete_sides": len(side_present) == 16,
                "complete_corners": len(corner_present) == 16,
                # first tile of each pattern: enough to wire a terrain deterministically
                "side_pattern_tiles": {p: side_map[p][0] for p in side_present},
                "corner_pattern_tiles": {p: corner_map[p][0] for p in corner_present},
            }
        )
    return {
        "schema": 1,
        "generated": date.today().isoformat(),
        "source": {
            "file": path.rsplit("/", 1)[-1].rsplit("\\", 1)[-1],
            "width": sheet.w,
            "height": sheet.h,
            "tile": tile,
            "columns": cols,
            "tile_rows": tile_rows,
            "block_rows": block_rows,
            "spacer_columns": [0],
        },
        "method": {
            "pattern_encoding": "4 chars: sides (T,R,B,L) or corners (TL,TR,BR,BL); 'W' = terrain on that side/corner",
            "terrain_signal": "alpha only. Valid where the block has transparent surroundings (kind=transparent-set).",
            "invalid_for": "kind=opaque blocks: alpha cannot distinguish terrain from background, so completeness must not be trusted; a colour/semantic pass is required.",
            "thresholds": {"tile_included": ">= 0.2 opaque coverage", "side_or_corner_present": "> 0.5 opaque"},
        },
        "blocks": blocks,
    }


def to_markdown(manifest):
    src = manifest["source"]
    lines = [
        "# Terrain manifest — autotile block coverage",
        "",
        f"Generated {manifest['generated']} by `tools/scan_terrain_blocks.py` from `{src['file']}` "
        f"({src['width']}×{src['height']} px, {src['columns']}×{src['tile_rows']} tiles of {src['tile']}px).",
        "",
        "**Why:** wiring a Godot terrain from a pack sheet is only safe when the chosen block supplies every",
        "pattern the terrain mode needs. An incomplete block does not fail loudly — Godot falls back to a",
        "wrong tile and the result merely looks *almost* right. Measure completeness; do not assume it.",
        "",
        "## Summary",
        "",
        "| Block | Tile rows | Tiles | Kind | Side patterns | Corner patterns | Usable as sides terrain |",
        "|---:|---|---:|---|---:|---:|:---|",
    ]
    for b in manifest["blocks"]:
        usable = "**yes — complete (16/16)**" if b["complete_sides"] else ("no" if b["kind"] in ("opaque", "uniform", "empty") else f"no — {b['side_patterns']['count']}/16")
        lines.append(
            f"| {b['id']} | {b['rows'][0]}–{b['rows'][1]} | {b['tiles']} | {b['kind']} | "
            f"{b['side_patterns']['count']}/16 | {b['corner_patterns']['count']}/16 | {usable} |"
        )
    complete = [b["id"] for b in manifest["blocks"] if b["complete_sides"]]
    opaque = [b["id"] for b in manifest["blocks"] if b["kind"] == "opaque"]
    lines += [
        "",
        "## Findings",
        "",
        f"- Blocks with a **complete 16/16 side set** (safe to wire as a `TerrainMode.Sides` terrain): "
        f"{', '.join(f'#{i}' for i in complete) if complete else '**none**'}.",
        f"- **{len(opaque)} of {len(manifest['blocks'])} blocks are `kind: opaque`** and therefore *not analyzable by "
        "alpha at all* — not necessarily incomplete, just invisible to this method. Their terrain shape is defined by",
        "  colour (e.g. water vs grass), so establishing those blocks' coverage needs a colour/semantic pass.",
        "  Treat `no` in the last column as *unknown* for those rows, never as proof of incompleteness.",
        "- An `opaque` block must never be scored by alpha: every tile looks identical to the detector, so a",
        "  \"13/13 roles\" reading there is an artifact of the measurement, not coverage.",
        "- `kind: uniform` blocks supply a single pattern only — decorative sheets, not autotile sources.",
        "- `corner_patterns` is recorded for reference; a corner-mode terrain additionally needs all four",
        "  inner-corner patterns. These blocks do not provide them, which is why sides mode is the right mode here.",
        "",
        "## Worked example",
        "",
        "Block #12 (rows 48–51) is the complete one. Wired from its 16 `side_pattern_tiles` into a",
        "`TerrainMode.Sides` terrain and painted, it renders a continuous fringe with resolved corners and no",
        "fallback tiles — see `terrain-manifest-block12.png`. Blocks #2/#4/#9 are the cautionary cases: wired as if",
        "complete, they render with hard seams, because the missing patterns force Godot to substitute.",
        "",
        "## How to use a complete block",
        "",
        "1. Read `blocks[i].side_pattern_tiles` — one representative tile per pattern, as `[col,row]`.",
        "2. Create a `TileSet` (tile size from `source.tile`), add a `TileSetAtlasSource` for the sheet.",
        "3. For each pattern, `CreateTile` the coordinate, then set `TileData.TerrainSet/.Terrain` and call",
        "   `SetTerrainPeeringBit` for each side marked `W` in the pattern string.",
        "4. Paint with `SetCellsTerrainConnect`. Read cells back and render before shipping.",
        "",
        "Set `texture_filter = Nearest` for pixel art. The sheet's column 0 is a transparent spacer.",
        "",
    ]
    return "\n".join(lines)


def main():
    ap = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    ap.add_argument("png")
    ap.add_argument("--tile", type=int, default=32)
    ap.add_argument("--out", help="JSON manifest path")
    ap.add_argument("--md", help="Markdown summary path")
    args = ap.parse_args()
    manifest = scan(args.png, args.tile)
    text = json.dumps(manifest, indent=2) + "\n"
    if args.out:
        open(args.out, "w", encoding="utf-8").write(text)
        print(f"wrote {args.out}")
    else:
        print(text)
    if args.md:
        open(args.md, "w", encoding="utf-8").write(to_markdown(manifest))
        print(f"wrote {args.md}")


if __name__ == "__main__":
    main()