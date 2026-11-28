import type { MapChunkBatch, MapChunk, MapManifest } from "../../contracts/hub";
import { MAP_CHUNK_SIZE } from "../../contracts/hub";

/**
 * Chunk-grid geometry for the viewport snapshot protocol.
 *
 * Mirrors `Genelife.Api.DTOs.MapChunkProtocol`: the chunk size is the ONE shared
 * constant, and the grid is anchored at the world's min corner (not the origin),
 * so `chunkToTile(0, 0)` is the world's min corner. Keeping this in one module
 * means the client never keeps a second `64` literal.
 */
export const CHUNK_SIZE = MAP_CHUNK_SIZE;

export interface TileRect {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

export interface ChunkAddress {
  cx: number;
  cy: number;
}

/** Inclusive chunk-address rectangle. */
export interface ChunkRect {
  cxMin: number;
  cyMin: number;
  cxMax: number;
  cyMax: number;
}

export function chunksPerSide(extent: number, chunkSize = CHUNK_SIZE): number {
  return extent <= 0 ? 0 : Math.ceil(extent / chunkSize);
}

export function chunkOrigin(worldMin: number, chunk: number, chunkSize = CHUNK_SIZE): number {
  return worldMin + chunk * chunkSize;
}

/** The world tile at a chunk's min corner: `MinX + cx * ChunkSize`. */
export function chunkToTile(
  cx: number,
  cy: number,
  worldMinX: number,
  worldMinY: number,
  chunkSize = CHUNK_SIZE
): { x: number; y: number } {
  return { x: chunkOrigin(worldMinX, cx, chunkSize), y: chunkOrigin(worldMinY, cy, chunkSize) };
}

/**
 * The chunk that contains a world tile. Floor division, so it is defined for
 * tiles left of / above the world's min corner (they land in negative chunks).
 */
export function tileToChunk(
  tileX: number,
  tileY: number,
  worldMinX: number,
  worldMinY: number,
  chunkSize = CHUNK_SIZE
): ChunkAddress {
  return {
    cx: Math.floor((tileX - worldMinX) / chunkSize),
    cy: Math.floor((tileY - worldMinY) / chunkSize)
  };
}

/** The world-tile bounds of one chunk, clamped to the world. */
export function chunkTileBounds(
  cx: number,
  cy: number,
  manifest: Pick<MapManifest, "minX" | "minY" | "maxX" | "maxY" | "chunkSize">
): TileRect {
  const minX = chunkOrigin(manifest.minX, cx, manifest.chunkSize);
  const minY = chunkOrigin(manifest.minY, cy, manifest.chunkSize);
  return {
    minX,
    minY,
    maxX: Math.min(minX + manifest.chunkSize - 1, manifest.maxX),
    maxY: Math.min(minY + manifest.chunkSize - 1, manifest.maxY)
  };
}

/**
 * The chunk set a camera rect needs, derived WITHOUT a round trip (E-6) and
 * clamped to the world's chunk grid. The client requests this whole set in one
 * batch rather than one request per chunk.
 */
export function chunksForTileRect(
  rect: TileRect,
  manifest: Pick<
    MapManifest,
    "minX" | "minY" | "maxX" | "maxY" | "chunkSize" | "chunksPerSideX" | "chunksPerSideY"
  >
): ChunkRect {
  const maxCx = Math.max(0, manifest.chunksPerSideX - 1);
  const maxCy = Math.max(0, manifest.chunksPerSideY - 1);

  const start = tileToChunk(rect.minX, rect.minY, manifest.minX, manifest.minY, manifest.chunkSize);
  const end = tileToChunk(rect.maxX, rect.maxY, manifest.minX, manifest.minY, manifest.chunkSize);

  return {
    cxMin: clamp(start.cx, 0, maxCx),
    cyMin: clamp(start.cy, 0, maxCy),
    cxMax: clamp(end.cx, 0, maxCx),
    cyMax: clamp(end.cy, 0, maxCy)
  };
}

/** Every address in a chunk rect, row-major. */
export function chunkAddresses(rect: ChunkRect): ChunkAddress[] {
  const addresses: ChunkAddress[] = [];
  for (let cy = rect.cyMin; cy <= rect.cyMax; cy++) {
    for (let cx = rect.cxMin; cx <= rect.cxMax; cx++) addresses.push({ cx, cy });
  }
  return addresses;
}

/** The chunk at `(cx, cy)` in a batch, or null. */
export function findChunk(batch: MapChunkBatch, cx: number, cy: number): MapChunk | null {
  return batch.chunks.find((chunk) => chunk.cx === cx && chunk.cy === cy) ?? null;
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(Math.max(value, min), max);
}
