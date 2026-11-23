import { hasFlag, inBounds, type DecodedMap } from "../../simulation/map/rle-decoder";

export interface TileVisual {
  terrainColor: number;
  terrainAlpha: number;
  road: boolean;
  roadColor: number;
  roadScaleX: number;
  roadScaleY: number;
}

const LAND_VARIANTS = [0x2f5d3a, 0x356640, 0x2b5536, 0x396b43];
const WATER = 0x1f4f73;
const ROAD = 0x4c5158;
const OCCUPIED = 0x27332b;

export function hashTile(x: number, y: number, seed: number): number {
  let hash = (x * 374761393 + y * 668265263 + seed * 2246822519) | 0;
  hash = (hash ^ (hash >>> 13)) * 1274126177;
  hash ^= hash >>> 16;
  return Math.abs(hash);
}

/** Road neighbour bitmask used to pick a straight, corner or intersection shape. */
export function roadMask(map: DecodedMap, x: number, y: number): number {
  let mask = 0;
  if (isRoad(map, x, y - 1)) mask |= 1;
  if (isRoad(map, x + 1, y)) mask |= 2;
  if (isRoad(map, x, y + 1)) mask |= 4;
  if (isRoad(map, x - 1, y)) mask |= 8;
  return mask;
}

export function tileVisual(map: DecodedMap, x: number, y: number): TileVisual {
  const index = (y - map.minY) * map.width + (x - map.minX);
  const bits = inBounds(map, x, y) ? (map.flags[index] ?? 0) : 0;
  const variant = hashTile(x, y, map.seed) % LAND_VARIANTS.length;

  if (hasFlag(bits, "Water")) {
    return {
      terrainColor: WATER,
      terrainAlpha: 1,
      road: false,
      roadColor: ROAD,
      roadScaleX: 1,
      roadScaleY: 1
    };
  }

  let terrainColor = LAND_VARIANTS[variant] ?? LAND_VARIANTS[0] ?? 0x2f5d3a;
  if (hasFlag(bits, "Occupied")) terrainColor = OCCUPIED;

  if (!hasFlag(bits, "Road")) {
    return {
      terrainColor,
      terrainAlpha: 1,
      road: false,
      roadColor: ROAD,
      roadScaleX: 1,
      roadScaleY: 1
    };
  }

  const mask = roadMask(map, x, y);
  const north = (mask & 1) !== 0;
  const east = (mask & 2) !== 0;
  const south = (mask & 4) !== 0;
  const west = (mask & 8) !== 0;
  const vertical = north || south;
  const horizontal = east || west;
  const both = vertical && horizontal;

  return {
    terrainColor,
    terrainAlpha: 1,
    road: true,
    roadColor: both ? 0x565c63 : ROAD,
    roadScaleX: both || horizontal ? 1 : 0.55,
    roadScaleY: both || vertical ? 1 : 0.55
  };
}

export function buildingColor(kind: string, seed: number): number {
  const palette: Record<string, number> = {
    house: 0x8a6a4a,
    residence: 0x8a6a4a,
    apartment: 0x7d6b5a,
    office: 0x516b86,
    shop: 0x9c7c45,
    grocery: 0x6f8d4f,
    school: 0x8f7f5c,
    hospital: 0x8a5b5b,
    factory: 0x6a6f78
  };
  const base = palette[kind.toLowerCase()];
  if (base !== undefined) return base;
  return 0x666f7a - (hashTile(kind.length, seed, 17) % 0x101010);
}

function isRoad(map: DecodedMap, x: number, y: number): boolean {
  if (!inBounds(map, x, y)) return false;
  const bits = map.flags[(y - map.minY) * map.width + (x - map.minX)] ?? 0;
  return (bits & 2) !== 0;
}
