import { Graphics, type Container } from "pixi.js";
import type { MapBuilding } from "../contracts/hub";
import type { DecodedMap } from "../simulation/map/rle-decoder";

export interface TilePoint {
  x: number;
  y: number;
}

/** Hover, target and building highlights. Always above genoses. */
export class SelectionLayer {
  private readonly hover = new Graphics();
  private readonly target = new Graphics();
  private readonly building = new Graphics();

  constructor(container: Container) {
    container.addChild(this.hover, this.building, this.target);
  }

  setHover(map: DecodedMap, tile: TilePoint | null, walkable: boolean): void {
    this.hover.clear();
    if (tile === null) {
      this.hover.visible = false;
      return;
    }
    const ts = map.tileSize;
    this.hover.visible = true;
    this.hover.position.set(tile.x * ts, tile.y * ts);
    this.hover
      .rect(-0.5 * ts, -0.5 * ts, ts, ts)
      .fill({ color: walkable ? 0x67e8f9 : 0xef4444, alpha: 0.2 })
      .stroke({ width: Math.max(0.04, ts * 0.06), color: walkable ? 0x67e8f9 : 0xef4444 });
  }

  setTarget(map: DecodedMap, tile: TilePoint | null): void {
    this.target.clear();
    if (tile === null) {
      this.target.visible = false;
      return;
    }
    const ts = map.tileSize;
    this.target.visible = true;
    this.target.position.set(tile.x * ts, tile.y * ts);
    this.target
      .circle(0, 0, ts * 0.55)
      .stroke({ width: Math.max(0.05, ts * 0.07), color: 0x34d399 })
      .moveTo(-ts * 0.7, 0)
      .lineTo(ts * 0.7, 0)
      .moveTo(0, -ts * 0.7)
      .lineTo(0, ts * 0.7)
      .stroke({ width: Math.max(0.03, ts * 0.04), color: 0x34d399, alpha: 0.8 });
  }

  setBuilding(map: DecodedMap, building: MapBuilding | null): void {
    this.building.clear();
    if (building === null) {
      this.building.visible = false;
      return;
    }
    const ts = map.tileSize;
    this.building.visible = true;
    this.building.position.set(0, 0);
    this.building
      .rect(
        (building.x - 0.5) * ts,
        (building.y - 0.5) * ts,
        building.width * ts,
        building.height * ts
      )
      .stroke({ width: Math.max(0.05, ts * 0.08), color: 0xffd166 })
      .circle(building.entryX * ts, building.entryY * ts, ts * 0.45)
      .stroke({ width: Math.max(0.04, ts * 0.06), color: 0xffd166 });
  }

  clear(): void {
    this.hover.clear();
    this.target.clear();
    this.building.clear();
    this.hover.visible = false;
    this.target.visible = false;
    this.building.visible = false;
  }

  destroy(): void {
    this.hover.destroy();
    this.target.destroy();
    this.building.destroy();
  }
}
