import { describe, expect, it } from "vitest";
import type { MapChunk, MapChunkBatch } from "../../contracts/hub";
import { CHUNK_SIZE } from "./chunk-protocol";
import { ChunkCache } from "./chunk-cache";

function landChunk(cx: number, cy: number): MapChunk {
  const tiles = [];
  for (let y = 0; y < CHUNK_SIZE; y++) tiles.push({ x: 0, y, length: CHUNK_SIZE, flags: "Land" });
  return { cx, cy, tiles };
}

function batch(specHash: string, chunks: MapChunk[]): MapChunkBatch {
  return {
    specHash,
    tileEncodingVersion: 1,
    chunkSize: CHUNK_SIZE,
    minX: -16000,
    minY: -16000,
    maxX: -16000 + CHUNK_SIZE - 1,
    maxY: -16000 + CHUNK_SIZE - 1,
    chunks,
    buildings: []
  };
}

describe("ChunkCache", () => {
  it("stores and returns a decoded chunk, keyed by (cx, cy, specHash)", () => {
    const cache = new ChunkCache();
    const entry = cache.put(landChunk(1, 2), "spec-a", CHUNK_SIZE);

    expect(entry.cx).toBe(1);
    expect(entry.flags).toHaveLength(CHUNK_SIZE * CHUNK_SIZE);
    expect(cache.get(1, 2, "spec-a")).toBe(entry);
    expect(cache.has(1, 2, "spec-a")).toBe(true);
    // A different spec hash is a different key (never a false hit).
    expect(cache.get(1, 2, "spec-b")).toBeNull();
  });

  it("returns null on a miss", () => {
    const cache = new ChunkCache();
    expect(cache.get(9, 9, "spec-a")).toBeNull();
  });

  it("purges the whole cache when the spec hash changes", () => {
    const cache = new ChunkCache({ specHash: "spec-a" });
    cache.put(landChunk(0, 0), "spec-a", CHUNK_SIZE);
    cache.put(landChunk(1, 0), "spec-a", CHUNK_SIZE);
    expect(cache.size).toBe(2);

    const purged = cache.observeSpecHash("spec-b");
    expect(purged).toBe(true);
    expect(cache.size).toBe(0);
    expect(cache.get(0, 0, "spec-a")).toBeNull();
    expect(cache.currentSpecHash).toBe("spec-b");

    expect(cache.observeSpecHash("spec-b")).toBe(false);
  });

  it("stores a whole batch", () => {
    const cache = new ChunkCache();
    const stored = cache.putBatch(batch("spec-a", [landChunk(0, 0), landChunk(1, 0), landChunk(2, 0)]));

    expect(stored).toHaveLength(3);
    expect(cache.size).toBe(3);
    expect(cache.currentSpecHash).toBe("spec-a");
  });

  it("evicts the least recently used chunk past the cap", () => {
    const cache = new ChunkCache({ maxChunks: 2 });
    cache.put(landChunk(0, 0), "spec-a", CHUNK_SIZE);
    cache.put(landChunk(1, 0), "spec-a", CHUNK_SIZE);
    // Touch (0,0) so (1,0) becomes the oldest.
    cache.get(0, 0, "spec-a");

    cache.put(landChunk(2, 0), "spec-a", CHUNK_SIZE);

    expect(cache.size).toBe(2);
    expect(cache.has(0, 0, "spec-a")).toBe(true);
    expect(cache.has(1, 0, "spec-a")).toBe(false);
    expect(cache.has(2, 0, "spec-a")).toBe(true);
  });

  it("reports only the addresses that are missing", () => {
    const cache = new ChunkCache();
    cache.putBatch(batch("spec-a", [landChunk(0, 0), landChunk(2, 0)]));

    const missing = cache.missing(
      [
        { cx: 0, cy: 0 },
        { cx: 1, cy: 0 },
        { cx: 2, cy: 0 }
      ],
      "spec-a"
    );

    expect(missing).toEqual([{ cx: 1, cy: 0 }]);
  });

  it("clears everything", () => {
    const cache = new ChunkCache();
    cache.putBatch(batch("spec-a", [landChunk(0, 0)]));
    cache.clear();
    expect(cache.size).toBe(0);
  });
});
