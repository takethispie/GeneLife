import { Sprite, Texture, type Container } from "pixi.js";
import type { Camera } from "../camera";
import type { DecodedMap } from "../../simulation/map/rle-decoder";
import { TileVisualCache } from "./tile-visual-cache";
import { DisplayPool } from "../pools";

export interface TileLayerStats {
  tiles: number;
  roads: number;
}

/**
 * Batched terrain and road sprites. Only visible tiles acquire pool objects, so
 * panning reuses sprites instead of rebuilding the world.
 */
export class TileLayer {
  private readonly terrainPool: DisplayPool<Sprite>;
  private readonly roadPool: DisplayPool<Sprite>;
  private readonly visualCache = new TileVisualCache();

  constructor(
    terrain: Container,
    roads: Container,
    texture: Texture = Texture.WHITE
  ) {
    this.terrainPool = new DisplayPool(() => {
      const sprite = new Sprite(texture);
      sprite.anchor.set(0.5);
      terrain.addChild(sprite);
      return sprite;
    });
    this.roadPool = new DisplayPool(() => {
      const sprite = new Sprite(texture);
      sprite.anchor.set(0.5);
      roads.addChild(sprite);
      return sprite;
    });
  }

  render(map: DecodedMap, camera: Camera): TileLayerStats {
    const rect = camera.visibleTileRect(map);
    const tileSize = map.tileSize;
    const cache = this.visualCache;
    cache.ensure(map);
    let roadCount = 0;

    this.terrainPool.begin();
    this.roadPool.begin();

    for (let y = rect.minY; y <= rect.maxY; y++) {
      for (let x = rect.minX; x <= rect.maxX; x++) {
        const index = cache.indexOf(map, x, y);
        const worldX = x * tileSize;
        const worldY = y * tileSize;

        const ground = this.terrainPool.acquire();
        ground.position.set(worldX, worldY);
        ground.width = tileSize;
        ground.height = tileSize;
        ground.tint = cache.terrain[index] ?? 0x2f5d3a;
        ground.alpha = 1;

        if (cache.road[index] === 1) {
          const road = this.roadPool.acquire();
          road.position.set(worldX, worldY);
          road.width = tileSize * ((cache.scaleX[index] ?? 100) / 100);
          road.height = tileSize * ((cache.scaleY[index] ?? 100) / 100);
          road.tint = cache.roadColor[index] ?? 0x4c5158;
          road.alpha = 0.95;
          roadCount++;
        }
      }
    }

    this.terrainPool.end();
    this.roadPool.end();

    return {
      tiles: this.terrainPool.activeCount,
      roads: roadCount
    };
  }

  destroy(): void {
    this.terrainPool.destroy();
    this.roadPool.destroy();
  }
}
