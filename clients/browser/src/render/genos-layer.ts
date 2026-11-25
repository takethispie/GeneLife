import { Container, Sprite, Text } from "pixi.js";
import type { Camera } from "./camera";
import type { RenderGenos } from "../simulation/interpolation";
import type { RenderState } from "../simulation/store";
import { AppearanceSpritePool } from "./appearance/sprite-pool";
import type { SceneTextures } from "./textures";

export interface GenosLayerStats {
  visible: number;
  total: number;
  localVisible: boolean;
}

const PLAYER_COLOR = 0xa78bfa;
const LOCAL_COLOR = 0x67e8f9;
const DEAD_COLOR = 0x6b7280;

/**
 * Renders interpolated genoses as pooled layered appearance sprites. The same
 * descriptor hash composes the same layers; position and scale are updated per
 * frame without reallocating textures.
 */
export class GenosLayer {
  private readonly appearance: AppearanceSpritePool;
  private readonly ring: Sprite;
  private readonly label: Text;

  constructor(container: Container, textures: SceneTextures) {
    container.sortableChildren = true;
    this.appearance = new AppearanceSpritePool(container, textures.genos);

    this.ring = new Sprite(textures.ring);
    this.ring.anchor.set(0.5);
    this.ring.tint = LOCAL_COLOR;
    this.ring.zIndex = 100;
    container.addChild(this.ring);

    this.label = new Text({
      text: "",
      style: {
        fontFamily: "system-ui, sans-serif",
        fontSize: 10,
        fill: 0xcffafe,
        stroke: { color: 0x07121a, width: 3 }
      }
    });
    this.label.anchor.set(0.5);
    this.label.zIndex = 101;
    container.addChild(this.label);
  }

  render(
    state: RenderState | null,
    ownGenosId: number,
    ownGenosName: string,
    camera: Camera,
    tileSize: number
  ): GenosLayerStats {
    this.appearance.begin();
    const baseSize = tileSize * 0.9;
    let visible = 0;
    let localVisible = false;
    let local: RenderGenos | null = null;

    for (const genos of state?.genoses.values() ?? []) {
      if (!camera.isWorldVisible(genos.x, genos.y, tileSize)) continue;

      const isLocal = genos.networkId === ownGenosId;
      const isPlayer = genos.isPlayerControlled === true || isLocal;
      const size = isLocal ? baseSize * 1.15 : baseSize;
      const { container } = this.appearance.acquire(genos.appearance, size, isPlayer, isLocal);
      container.position.set(genos.x, genos.y);
      visible++;

      if (isLocal) {
        localVisible = true;
        local = genos;
      }
    }

    this.appearance.end();

    if (local !== null) {
      this.ring.visible = true;
      this.ring.position.set(local.x, local.y);
      this.ring.width = tileSize * 1.45;
      this.ring.height = tileSize * 1.45;

      this.label.visible = true;
      this.label.text = ownGenosName === "" ? "You" : ownGenosName;
      this.label.position.set(local.x, local.y - tileSize * 0.95);
      this.label.scale.set(1 / camera.zoom);
    } else {
      this.ring.visible = false;
      this.label.visible = false;
    }

    return {
      visible,
      total: state?.genoses.size ?? 0,
      localVisible
    };
  }

  renderNoWorld(): GenosLayerStats {
    this.appearance.begin();
    this.appearance.end();
    this.ring.visible = false;
    this.label.visible = false;
    return { visible: 0, total: 0, localVisible: false };
  }

  destroy(): void {
    this.appearance.destroy();
    this.ring.destroy();
    this.label.destroy();
  }
}

export { DEAD_COLOR, PLAYER_COLOR };
