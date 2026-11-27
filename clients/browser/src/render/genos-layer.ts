import { Container, Sprite, Text } from "pixi.js";
import type { Camera } from "./camera";
import type { RenderGenos } from "../simulation/interpolation";
import type { RenderState } from "../simulation/store";
import { AppearanceSpritePool } from "./appearance/sprite-pool";
import { DisplayPool } from "./pools";
import type { SceneTextures } from "./textures";

export interface GenosLayerStats {
  visible: number;
  total: number;
  labels: number;
  localVisible: boolean;
}

const PLAYER_COLOR = 0xa78bfa;
const LOCAL_COLOR = 0x67e8f9;
const DEAD_COLOR = 0x6b7280;

/** Label offset above a genos' feet, in tiles. */
const LABEL_OFFSET_TILES = 0.95;

/**
 * Renders interpolated genoses as pooled layered appearance sprites. The same
 * descriptor hash composes the same layers; position and scale are updated per
 * frame without reallocating textures.
 *
 * Every genos visible in the viewport gets its name above its head. Non-local
 * labels come from a `DisplayPool` so panning reuses the same `Text` objects
 * instead of allocating one per genos per frame. The local genos keeps its own
 * distinct label (its own colour) and the ring, and falls back to "You".
 */
export class GenosLayer {
  private readonly appearance: AppearanceSpritePool;
  private readonly ring: Sprite;
  private readonly localLabel: Text;
  private readonly labelPool: DisplayPool<Text>;

  constructor(container: Container, textures: SceneTextures) {
    container.sortableChildren = true;
    this.appearance = new AppearanceSpritePool(container, textures.genos);

    this.ring = new Sprite(textures.ring);
    this.ring.anchor.set(0.5);
    this.ring.tint = LOCAL_COLOR;
    this.ring.zIndex = 100;
    container.addChild(this.ring);

    // The local genos label is separate from the pool so it can carry its own
    // colour and stay visually distinguishable from every other genos' name.
    this.localLabel = new Text({
      text: "",
      style: {
        fontFamily: "system-ui, sans-serif",
        fontSize: 10,
        fill: LOCAL_COLOR,
        stroke: { color: 0x07121a, width: 3 }
      }
    });
    this.localLabel.anchor.set(0.5);
    this.localLabel.zIndex = 102;
    container.addChild(this.localLabel);

    this.labelPool = new DisplayPool(() => {
      const label = new Text({
        text: "",
        style: {
          fontFamily: "system-ui, sans-serif",
          fontSize: 10,
          fill: 0xcffafe,
          stroke: { color: 0x07121a, width: 3 }
        }
      });
      label.anchor.set(0.5);
      label.zIndex = 101;
      container.addChild(label);
      return label;
    });
  }

  render(
    state: RenderState | null,
    ownGenosId: number,
    nameOf: (networkId: number) => string,
    camera: Camera,
    tileSize: number
  ): GenosLayerStats {
    this.appearance.begin();
    this.labelPool.begin();
    const baseSize = tileSize * 0.9;
    const labelScale = 1 / camera.zoom;
    let visible = 0;
    let labels = 0;
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
        continue;
      }

      // Nothing to show for an unnamed genos; the pool skips it so the label
      // count reflects the names actually drawn.
      const name = nameOf(genos.networkId);
      if (name === "") continue;

      const label = this.labelPool.acquire();
      if (label.text !== name) label.text = name;
      label.position.set(genos.x, genos.y - tileSize * LABEL_OFFSET_TILES);
      label.scale.set(labelScale);
      labels++;
    }

    this.appearance.end();
    this.labelPool.end();

    if (local !== null) {
      this.ring.visible = true;
      this.ring.position.set(local.x, local.y);
      this.ring.width = tileSize * 1.45;
      this.ring.height = tileSize * 1.45;

      this.localLabel.visible = true;
      const localName = nameOf(local.networkId);
      const text = localName === "" ? "You" : localName;
      if (this.localLabel.text !== text) this.localLabel.text = text;
      this.localLabel.position.set(local.x, local.y - tileSize * LABEL_OFFSET_TILES);
      this.localLabel.scale.set(labelScale);
      labels++;
    } else {
      this.ring.visible = false;
      this.localLabel.visible = false;
    }

    return {
      visible,
      total: state?.genoses.size ?? 0,
      labels,
      localVisible
    };
  }

  renderNoWorld(): GenosLayerStats {
    this.appearance.begin();
    this.appearance.end();
    this.labelPool.begin();
    this.labelPool.end();
    this.ring.visible = false;
    this.localLabel.visible = false;
    return { visible: 0, total: 0, labels: 0, localVisible: false };
  }

  destroy(): void {
    this.appearance.destroy();
    this.ring.destroy();
    this.localLabel.destroy();
    this.labelPool.destroy();
  }
}

export { DEAD_COLOR, PLAYER_COLOR };
