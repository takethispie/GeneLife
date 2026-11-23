import type { TileRect } from "../net/viewport-client";

export interface CullRect {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

export function intersects(a: CullRect, b: CullRect): boolean {
  return a.minX <= b.maxX && a.maxX >= b.minX && a.minY <= b.maxY && a.maxY >= b.minY;
}

export function tileRectToWorld(
  rect: TileRect,
  tileSize: number
): CullRect {
  return {
    minX: (rect.minX - 0.5) * tileSize,
    minY: (rect.minY - 0.5) * tileSize,
    maxX: (rect.maxX + 0.5) * tileSize,
    maxY: (rect.maxY + 0.5) * tileSize
  };
}

export function containsPoint(rect: CullRect, x: number, y: number): boolean {
  return x >= rect.minX && x <= rect.maxX && y >= rect.minY && y <= rect.maxY;
}
