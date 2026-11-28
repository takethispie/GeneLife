import type { MapChunk, MapChunkBatch } from "../../contracts/hub";
import { decodeChunkTiles } from "./rle-decoder";
import type { ChunkAddress } from "./chunk-protocol";

/** A decoded chunk: a bounded `chunkSize × chunkSize` flag array. */
export interface CachedChunk {
  cx: number;
  cy: number;
  specHash: string;
  chunkSize: number;
  /** Row-major flags, `y * chunkSize + x`. */
  flags: Uint8Array;
}

export interface ChunkCacheOptions {
  /** Maximum live chunks; the oldest are evicted first (LRU). */
  maxChunks?: number;
  specHash?: string | null;
}

/**
 * A bounded LRU cache of decoded chunks, keyed by `(cx, cy, specHash)`.
 *
 * Determinism is what makes this correct: for a given `(specHash, cx, cy)` the
 * server's payload is byte-stable, so a cached chunk never silently changes. A
 * spec-hash CHANGE is a full purge — the world was regenerated and every cached
 * chunk belongs to a world that no longer exists. The client never holds a
 * whole-map array; the cache is the only place tiles live.
 */
export class ChunkCache {
  private readonly maxChunks: number;
  private specHash: string | null;
  private readonly entries = new Map<string, CachedChunk>();

  constructor(options: ChunkCacheOptions = {}) {
    this.maxChunks = Math.max(1, options.maxChunks ?? 256);
    this.specHash = options.specHash ?? null;
  }

  get size(): number {
    return this.entries.size;
  }

  get currentSpecHash(): string | null {
    return this.specHash;
  }

  static key(cx: number, cy: number, specHash: string): string {
    return `${cx},${cy},${specHash}`;
  }

  /** The cached chunk, or `null` on a miss or a spec mismatch. Touches the LRU. */
  get(cx: number, cy: number, specHash: string | null = this.specHash): CachedChunk | null {
    if (specHash === null) return null;
    const key = ChunkCache.key(cx, cy, specHash);
    const entry = this.entries.get(key);
    if (entry === undefined) return null;
    this.entries.delete(key);
    this.entries.set(key, entry);
    return entry;
  }

  has(cx: number, cy: number, specHash: string | null = this.specHash): boolean {
    return this.get(cx, cy, specHash) !== null;
  }

  /** Which of the addresses are absent for a spec hash, so only those need fetching. */
  missing(addresses: ChunkAddress[], specHash: string | null = this.specHash): ChunkAddress[] {
    return addresses.filter((address) => !this.has(address.cx, address.cy, specHash));
  }

  /** Decodes and stores one chunk. */
  put(chunk: MapChunk, specHash: string, chunkSize: number): CachedChunk {
    this.observeSpecHash(specHash);
    const entry: CachedChunk = {
      cx: chunk.cx,
      cy: chunk.cy,
      specHash,
      chunkSize,
      flags: decodeChunkTiles(chunk, chunkSize)
    };
    const key = ChunkCache.key(chunk.cx, chunk.cy, specHash);
    this.entries.delete(key);
    this.entries.set(key, entry);
    this.evict();
    return entry;
  }

  /** Decodes and stores every chunk of a batch. */
  putBatch(batch: MapChunkBatch): CachedChunk[] {
    return batch.chunks.map((chunk) => this.put(chunk, batch.specHash, batch.chunkSize));
  }

  /**
   * Adopt a spec hash. Returns true when it CHANGED, which purges the whole
   * cache: a regenerated world invalidates every cached chunk at once.
   */
  observeSpecHash(specHash: string): boolean {
    if (this.specHash === specHash) return false;
    this.specHash = specHash;
    this.entries.clear();
    return true;
  }

  clear(): void {
    this.entries.clear();
  }

  private evict(): void {
    while (this.entries.size > this.maxChunks) {
      const oldest = this.entries.keys().next().value;
      if (oldest === undefined) return;
      this.entries.delete(oldest);
    }
  }
}
