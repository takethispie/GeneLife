import { Sprite, Text, Texture, type Container } from "pixi.js";
import type { Camera } from "../camera";
import type { DecodedMap } from "../../simulation/map/rle-decoder";
import { DisplayPool } from "../pools";
import { buildingColor } from "./tileset";

export interface BuildingLayerStats {
  buildings: number;
  entries: number;
  labels: number;
}

/**
 * Building footprints, roofs, entry markers and close-zoom labels. Occupancy
 * comes from the building list, never from tile flags.
 */
export class BuildingLayer {
  private readonly bodyPool: DisplayPool<Sprite>;
  private readonly roofPool: DisplayPool<Sprite>;
  private readonly entryPool: DisplayPool<Sprite>;
  private readonly labelPool: DisplayPool<Text>;

  constructor(
    buildings: Container,
    effects: Container,
    textures: { white: Texture; circle: Texture }
  ) {
    this.bodyPool = new DisplayPool(() => {
      const sprite = new Sprite(textures.white);
      sprite.anchor.set(0.5);
      buildings.addChild(sprite);
      return sprite;
    });
    this.roofPool = new DisplayPool(() => {
      const sprite = new Sprite(textures.white);
      sprite.anchor.set(0.5);
      buildings.addChild(sprite);
      return sprite;
    });
    this.entryPool = new DisplayPool(() => {
      const sprite = new Sprite(textures.circle);
      sprite.anchor.set(0.5);
      effects.addChild(sprite);
      return sprite;
    });
    this.labelPool = new DisplayPool(() => {
      const label = new Text({
        text: "",
        style: {
          fontFamily: "system-ui, sans-serif",
          fontSize: 10,
          fill: 0xe6edf3,
          stroke: { color: 0x0b1014, width: 3 }
        }
      });
      label.anchor.set(0.5);
      buildings.addChild(label);
      return label;
    });
  }

  render(map: DecodedMap, camera: Camera): BuildingLayerStats {
    const tileSize = map.tileSize;
    const showLabels = camera.zoom >= 1.6;
    let count = 0;
    let entries = 0;
    let labels = 0;

    this.bodyPool.begin();
    this.roofPool.begin();
    this.entryPool.begin();
    this.labelPool.begin();

    for (const building of map.buildings.values()) {
      const centerX = (building.x + building.width / 2 - 0.5) * tileSize;
      const centerY = (building.y + building.height / 2 - 0.5) * tileSize;
      const width = building.width * tileSize;
      const height = building.height * tileSize;

      if (!camera.isWorldVisible(centerX, centerY, Math.max(width, height))) continue;

      const color = buildingColor(building.kind, map.seed);
      const body = this.bodyPool.acquire();
      body.position.set(centerX, centerY);
      body.width = width;
      body.height = height;
      body.tint = color;

      const roof = this.roofPool.acquire();
      roof.position.set(centerX, centerY);
      roof.width = width * 0.78;
      roof.height = height * 0.7;
      roof.tint = lighten(color, 1.22);
      count++;

      const entry = this.entryPool.acquire();
      entry.position.set(building.entryX * tileSize, building.entryY * tileSize);
      entry.width = tileSize * 0.5;
      entry.height = tileSize * 0.5;
      entry.tint = 0xffd166;
      entry.alpha = 0.9;
      entries++;

      if (showLabels) {
        const label = this.labelPool.acquire();
        label.text = building.name;
        label.position.set(centerX, centerY - height * 0.65);
        label.scale.set(1 / camera.zoom);
        label.alpha = 0.92;
        labels++;
      }
    }

    this.bodyPool.end();
    this.roofPool.end();
    this.entryPool.end();
    this.labelPool.end();

    return { buildings: count, entries, labels };
  }

  destroy(): void {
    this.bodyPool.destroy();
    this.roofPool.destroy();
    this.entryPool.destroy();
    this.labelPool.destroy();
  }
}

function lighten(color: number, factor: number): number {
  const r = Math.min(255, Math.round(((color >> 16) & 0xff) * factor));
  const g = Math.min(255, Math.round(((color >> 8) & 0xff) * factor));
  const b = Math.min(255, Math.round((color & 0xff) * factor));
  return (r << 16) | (g << 8) | b;
}
