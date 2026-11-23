import type { DecodedMap } from "../../simulation/map/rle-decoder";
import { tileVisual } from "./tileset";

/**
 * Immutable per-tile presentation data cached per map generation. Terrain flags
 * only change when the decoded map is replaced, so the expensive road-mask and
 * variant pass runs once instead of on every frame.
 */
export class TileVisualCache {
  terrain = new Uint32Array(0);
  roadColor = new Uint32Array(0);
  road = new Uint8Array(0);
  /** Percent of a tile, 0..100. */
  scaleX = new Uint8Array(0);
  scaleY = new Uint8Array(0);

  private flagsSource: Uint8Array | null = null;
  private seed = -1;

  ensure(map: DecodedMap): void {
    if (this.flagsSource === map.flags && this.seed === map.seed) return;

    const total = map.width * map.height;
    this.terrain = new Uint32Array(total);
    this.roadColor = new Uint32Array(total);
    this.road = new Uint8Array(total);
    this.scaleX = new Uint8Array(total);
    this.scaleY = new Uint8Array(total);
    this.flagsSource = map.flags;
    this.seed = map.seed;

    for (let y = map.minY; y <= map.maxY; y++) {
      for (let x = map.minX; x <= map.maxX; x++) {
        const index = (y - map.minY) * map.width + (x - map.minX);
        const visual = tileVisual(map, x, y);
        this.terrain[index] = visual.terrainColor;
        this.roadColor[index] = visual.roadColor;
        this.road[index] = visual.road ? 1 : 0;
        this.scaleX[index] = Math.round(visual.roadScaleX * 100);
        this.scaleY[index] = Math.round(visual.roadScaleY * 100);
      }
    }
  }

  indexOf(map: Pick<DecodedMap, "width" | "minX" | "minY">, x: number, y: number): number {
    return (y - map.minY) * map.width + (x - map.minX);
  }
}
