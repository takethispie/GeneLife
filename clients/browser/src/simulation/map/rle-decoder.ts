import type { MapBuilding, MapChunk, MapSnapshot, TileFlag } from "../../contracts/hub";
import { MAP_CHUNK_SIZE } from "../../contracts/hub";
import { ContractError } from "../../contracts/validate";

/** Bit values mirror `Genelife.Map.TileFlags`. */
export const TILE_FLAGS: Record<TileFlag, number> = {
  Land: 1,
  Road: 2,
  Occupied: 4,
  Reserved: 8,
  Water: 16
};

export interface DecodedMap {
  mapId: string;
  version: number;
  seed: number;
  width: number;
  height: number;
  tileSize: number;
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
  /** Row-major flag bits, `(y - minY) * width + (x - minX)`. */
  flags: Uint8Array;
  buildings: Map<number, MapBuilding>;
}

export function parseTileFlags(flags: string, path = "tileFlags"): number {
  if (flags.trim() === "" || flags.trim() === "None") return 0;

  let bits = 0;
  for (const raw of flags.split(",")) {
    const name = raw.trim();
    if (name === "" || name === "None") continue;
    const value = TILE_FLAGS[name as TileFlag];
    if (value === undefined) {
      throw new ContractError(`unknown tile flag "${name}"`, path);
    }
    bits |= value;
  }
  return bits;
}

export function tileIndex(map: Pick<DecodedMap, "width" | "minX" | "minY">, x: number, y: number): number {
  return (y - map.minY) * map.width + (x - map.minX);
}

export function inBounds(
  map: Pick<DecodedMap, "minX" | "minY" | "maxX" | "maxY">,
  x: number,
  y: number
): boolean {
  return x >= map.minX && x <= map.maxX && y >= map.minY && y <= map.maxY;
}

export function flagsAt(map: DecodedMap, x: number, y: number): number {
  if (!inBounds(map, x, y)) return 0;
  return map.flags[tileIndex(map, x, y)] ?? 0;
}

export function hasFlag(bits: number, flag: TileFlag): boolean {
  return (bits & TILE_FLAGS[flag]) !== 0;
}

/**
 * Local movement hint used for pointer feedback only. The server remains the
 * authority for walkability and occupancy.
 */
export function isWalkableHint(map: DecodedMap, x: number, y: number): boolean {
  if (!inBounds(map, x, y)) return false;
  const bits = flagsAt(map, x, y);
  return !hasFlag(bits, "Water") && !hasFlag(bits, "Occupied");
}

export function buildingAt(map: DecodedMap, x: number, y: number): MapBuilding | null {
  for (const building of map.buildings.values()) {
    if (
      x >= building.x &&
      x < building.x + building.width &&
      y >= building.y &&
      y < building.y + building.height
    ) {
      return building;
    }
  }
  return null;
}

/**
 * Decodes the per-row RLE layout into a dense flag index exactly once.
 *
 * Fails closed on gaps, overlaps, out-of-bounds runs and non-positive lengths.
 */
export function decodeMap(snapshot: MapSnapshot): DecodedMap {
  const width = snapshot.width;
  const height = snapshot.height;
  if (width <= 0 || height <= 0) {
    throw new ContractError("width and height must be positive", "mapSnapshot");
  }
  if (snapshot.tileSize <= 0) {
    throw new ContractError("tileSize must be positive", "mapSnapshot");
  }

  const total = width * height;
  const flags = new Uint8Array(total);
  const visited = new Uint8Array(total);
  let covered = 0;

  for (let runIndex = 0; runIndex < snapshot.tiles.length; runIndex++) {
    const run = snapshot.tiles[runIndex];
    if (run === undefined) continue;
    const path = `mapSnapshot.tiles[${runIndex}]`;

    if (!Number.isInteger(run.length) || run.length <= 0) {
      throw new ContractError("run length must be a positive integer", path);
    }
    if (!inBounds(snapshot, run.x, run.y)) {
      throw new ContractError(`run starts outside the map (${run.x}, ${run.y})`, path);
    }
    if (run.x + run.length - 1 > snapshot.maxX) {
      throw new ContractError("run extends past the right map edge", path);
    }

    const bits = parseTileFlags(run.flags, `${path}.flags`);
    const rowStart = (run.y - snapshot.minY) * width;

    for (let offset = 0; offset < run.length; offset++) {
      const index = rowStart + (run.x - snapshot.minX) + offset;
      if (index < 0 || index >= total) {
        throw new ContractError("run index outside the tile array", path);
      }
      if (visited[index] === 1) {
        throw new ContractError(`overlapping runs at (${run.x + offset}, ${run.y})`, path);
      }
      visited[index] = 1;
      flags[index] = bits;
      covered++;
    }
  }

  if (covered !== total) {
    throw new ContractError(
      `RLE coverage ${covered} does not equal width * height ${total}`,
      "mapSnapshot.tiles"
    );
  }

  const buildings = new Map<number, MapBuilding>();
  for (const building of snapshot.buildings) {
    if (buildings.has(building.networkId)) {
      throw new ContractError(
        `duplicate building network id ${building.networkId}`,
        "mapSnapshot.buildings"
      );
    }
    buildings.set(building.networkId, building);
  }

  return {
    mapId: snapshot.mapId,
    version: snapshot.version,
    seed: snapshot.seed,
    width,
    height,
    tileSize: snapshot.tileSize,
    minX: snapshot.minX,
    minY: snapshot.minY,
    maxX: snapshot.maxX,
    maxY: snapshot.maxY,
    flags,
    buildings
  };
}

/**
 * Decodes one chunk's RLE runs into a bounded `chunkSize × chunkSize` flag array.
 *
 * Runs are in CHUNK-LOCAL coordinates, so `index = y * chunkSize + x`. Nothing
 * world-sized is allocated: this is the decode the browser chunk cache fills.
 * Fails closed on gaps, overlaps, out-of-chunk runs and non-positive lengths,
 * exactly like {@link decodeMap}.
 */
export function decodeChunkTiles(chunk: MapChunk, chunkSize: number = MAP_CHUNK_SIZE): Uint8Array {
  if (!Number.isInteger(chunkSize) || chunkSize <= 0) {
    throw new ContractError("chunkSize must be a positive integer", "chunk");
  }

  const total = chunkSize * chunkSize;
  const flags = new Uint8Array(total);
  const visited = new Uint8Array(total);
  let covered = 0;

  for (let runIndex = 0; runIndex < chunk.tiles.length; runIndex++) {
    const run = chunk.tiles[runIndex];
    if (run === undefined) continue;
    const path = `chunk.tiles[${runIndex}]`;

    if (!Number.isInteger(run.x) || !Number.isInteger(run.y)) {
      throw new ContractError("run x/y must be integers", path);
    }
    if (!Number.isInteger(run.length) || run.length <= 0) {
      throw new ContractError("run length must be a positive integer", path);
    }
    if (run.x < 0 || run.y < 0 || run.x >= chunkSize || run.y >= chunkSize) {
      throw new ContractError(`run starts outside the chunk (${run.x}, ${run.y})`, path);
    }
    if (run.x + run.length - 1 >= chunkSize) {
      throw new ContractError("run extends past the chunk's right edge", path);
    }

    const bits = parseTileFlags(run.flags, `${path}.flags`);
    const rowStart = run.y * chunkSize;

    for (let offset = 0; offset < run.length; offset++) {
      const index = rowStart + run.x + offset;
      if (visited[index] === 1) {
        throw new ContractError(`overlapping runs at (${run.x + offset}, ${run.y})`, path);
      }
      visited[index] = 1;
      flags[index] = bits;
      covered++;
    }
  }

  if (covered !== total) {
    throw new ContractError(
      `chunk RLE coverage ${covered} does not equal chunkSize * chunkSize ${total}`,
      "chunk.tiles"
    );
  }

  return flags;
}

/** Row-major index inside one chunk's flag array. */
export function chunkTileIndex(chunkSize: number, x: number, y: number): number {
  return y * chunkSize + x;
}
