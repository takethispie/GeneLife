import { describe, expect, it } from "vitest";
import type { MapChunk } from "../../contracts/hub";
import {
  CHUNK_SIZE,
  chunkAddresses,
  chunkOrigin,
  chunkTileBounds,
  chunkToTile,
  chunksForTileRect,
  chunksPerSide,
  findChunk,
  tileToChunk
} from "./chunk-protocol";
import { decodeChunkTiles } from "./rle-decoder";

/** A fully-land 64×64 chunk: one per-row run. */
function landChunk(cx = 0, cy = 0, chunkSize = CHUNK_SIZE): MapChunk {
  const tiles = [];
  for (let y = 0; y < chunkSize; y++) {
    tiles.push({ x: 0, y, length: chunkSize, flags: "Land" });
  }
  return { cx, cy, tiles };
}

describe("chunk grid geometry", () => {
  it("divides the 32000 world cleanly", () => {
    expect(CHUNK_SIZE).toBe(64);
    expect(chunksPerSide(32000)).toBe(500);
    expect(chunksPerSide(256)).toBe(4);
  });

  it("anchors the grid at the world min corner", () => {
    expect(chunkToTile(0, 0, -16000, -16000)).toEqual({ x: -16000, y: -16000 });
    expect(chunkToTile(0, 1, -16000, -16000)).toEqual({ x: -16000, y: -15936 });
    expect(chunkOrigin(-16000, 499)).toBe(15936);
  });

  it("maps a tile back to its chunk with floor division", () => {
    expect(tileToChunk(-16000, -16000, -16000, -16000)).toEqual({ cx: 0, cy: 0 });
    expect(tileToChunk(-16000, -15936, -16000, -16000)).toEqual({ cx: 0, cy: 1 });
    // Just left of the world min is chunk -1, never chunk 0.
    expect(tileToChunk(-16001, -16000, -16000, -16000)).toEqual({ cx: -1, cy: 0 });
  });

  it("clamps a chunk's tile bounds to the world", () => {
    const bounds = chunkTileBounds(499, 499, {
      minX: -16000,
      minY: -16000,
      maxX: 15999,
      maxY: 15999,
      chunkSize: CHUNK_SIZE
    });
    expect(bounds).toEqual({ minX: 15936, minY: 15936, maxX: 15999, maxY: 15999 });
  });
});

describe("chunksForTileRect", () => {
  const manifest = {
    minX: -16000,
    minY: -16000,
    maxX: 15999,
    maxY: 15999,
    chunkSize: CHUNK_SIZE,
    chunksPerSideX: 500,
    chunksPerSideY: 500
  };

  it("derives the chunk set from a camera rect without a round trip", () => {
    const rect = chunksForTileRect({ minX: -16000, minY: -16000, maxX: -16000 + 130, maxY: -16000 + 130 }, manifest);
    expect(rect).toEqual({ cxMin: 0, cyMin: 0, cxMax: 2, cyMax: 2 });
    expect(chunkAddresses(rect)).toHaveLength(9);
  });

  it("clamps a rect that reaches past the world edge", () => {
    const rect = chunksForTileRect({ minX: 15999, minY: 15999, maxX: 20000, maxY: 20000 }, manifest);
    expect(rect).toEqual({ cxMin: 499, cyMin: 499, cxMax: 499, cyMax: 499 });
  });
});

describe("findChunk", () => {
  it("finds a chunk by address and returns null otherwise", () => {
    const batch = {
      specHash: "abc",
      tileEncodingVersion: 1,
      chunkSize: CHUNK_SIZE,
      minX: -16000,
      minY: -16000,
      maxX: -16000 + CHUNK_SIZE - 1,
      maxY: -16000 + CHUNK_SIZE - 1,
      chunks: [landChunk(0, 0), landChunk(1, 0)],
      buildings: []
    };
    expect(findChunk(batch, 1, 0)?.cx).toBe(1);
    expect(findChunk(batch, 5, 5)).toBeNull();
  });
});

describe("decodeChunkTiles", () => {
  it("decodes chunk-local runs into a bounded flag array", () => {
    const flags = decodeChunkTiles(landChunk());
    expect(flags).toHaveLength(CHUNK_SIZE * CHUNK_SIZE);
    expect(flags[0]).toBe(1);
    expect(flags[CHUNK_SIZE * CHUNK_SIZE - 1]).toBe(1);
  });

  it("respects an explicit chunk size", () => {
    const chunk: MapChunk = {
      cx: 0,
      cy: 0,
      tiles: [
        { x: 0, y: 0, length: 4, flags: "Water" },
        { x: 0, y: 1, length: 4, flags: "Land, Road" },
        { x: 0, y: 2, length: 4, flags: "Land" },
        { x: 0, y: 3, length: 4, flags: "Land" }
      ]
    };
    const flags = decodeChunkTiles(chunk, 4);
    expect(flags).toHaveLength(16);
    expect(flags[0]).toBe(16);
    expect(flags[4]).toBe(3);
  });

  it("rejects a gap in coverage", () => {
    const chunk = landChunk();
    chunk.tiles = chunk.tiles.slice(1);
    expect(() => decodeChunkTiles(chunk)).toThrowError(/coverage/);
  });

  it("rejects overlapping runs", () => {
    const chunk = landChunk();
    chunk.tiles = [...chunk.tiles, { x: 0, y: 0, length: 1, flags: "Land" }];
    expect(() => decodeChunkTiles(chunk)).toThrowError(/overlapping runs/);
  });

  it("rejects a run that extends past the chunk's right edge", () => {
    const chunk = landChunk();
    chunk.tiles = [
      ...chunk.tiles.slice(1),
      { x: CHUNK_SIZE - 1, y: 0, length: 2, flags: "Land" }
    ];
    expect(() => decodeChunkTiles(chunk)).toThrowError(/past the chunk's right edge/);
  });

  it("rejects a run that starts outside the chunk", () => {
    const chunk = landChunk();
    chunk.tiles = [...chunk.tiles.slice(1), { x: CHUNK_SIZE, y: 0, length: 1, flags: "Land" }];
    expect(() => decodeChunkTiles(chunk)).toThrowError(/starts outside the chunk/);
  });

  it("rejects a non-positive run length", () => {
    const chunk = landChunk();
    chunk.tiles = [...chunk.tiles.slice(1), { x: 0, y: 0, length: 0, flags: "Land" }];
    expect(() => decodeChunkTiles(chunk)).toThrowError(/run length must be a positive integer/);
  });
});
