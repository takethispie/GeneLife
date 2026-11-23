import { Sprite, Text, Texture, type Container } from "pixi.js";
import type { Camera } from "./camera";
import type { RenderSim } from "../simulation/interpolation";
import type { RenderState } from "../simulation/store";
import { DisplayPool } from "./pools";

export interface SimLayerStats {
  visible: number;
  total: number;
  localVisible: boolean;
}

const NPC_COLOR = 0xf2b544;
const PLAYER_COLOR = 0xa78bfa;
const LOCAL_COLOR = 0x67e8f9;
const DEAD_COLOR = 0x6b7280;

/**
 * Renders interpolated sim positions as pooled sprites. The local player is
 * larger, ringed and always drawn above other sims.
 */
export class SimLayer {
  private readonly pool: DisplayPool<Sprite>;
  private readonly ring: Sprite;
  private readonly label: Text;

  constructor(
    container: Container,
    textures: { circle: Texture; ring: Texture }
  ) {
    container.sortableChildren = true;
    this.pool = new DisplayPool(() => {
      const sprite = new Sprite(textures.circle);
      sprite.anchor.set(0.5);
      container.addChild(sprite);
      return sprite;
    });

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
    ownSimId: number,
    ownSimName: string,
    camera: Camera,
    tileSize: number
  ): SimLayerStats {
    this.pool.begin();
    const size = tileSize * 0.78;
    let visible = 0;
    let localVisible = false;
    let local: RenderSim | null = null;

    for (const sim of state?.sims.values() ?? []) {
      if (!camera.isWorldVisible(sim.x, sim.y, tileSize)) continue;

      const sprite = this.pool.acquire();
      const isLocal = sim.networkId === ownSimId;
      const isPlayer = isLocal || false;
      sprite.position.set(sim.x, sim.y);
      sprite.width = isLocal ? size * 1.25 : size;
      sprite.height = isLocal ? size * 1.25 : size;
      sprite.tint = isLocal ? LOCAL_COLOR : isPlayer ? PLAYER_COLOR : NPC_COLOR;
      sprite.alpha = isLocal ? 1 : 0.92;
      sprite.zIndex = isLocal ? 50 : 10;
      visible++;

      if (isLocal) {
        localVisible = true;
        local = sim;
      }
    }

    this.pool.end();

    if (local !== null) {
      this.ring.visible = true;
      this.ring.position.set(local.x, local.y);
      this.ring.width = tileSize * 1.4;
      this.ring.height = tileSize * 1.4;

      this.label.visible = true;
      this.label.text = ownSimName === "" ? "You" : ownSimName;
      this.label.position.set(local.x, local.y - tileSize * 0.95);
      this.label.scale.set(1 / camera.zoom);
    } else {
      this.ring.visible = false;
      this.label.visible = false;
    }

    return {
      visible,
      total: state?.sims.size ?? 0,
      localVisible
    };
  }

  renderNoWorld(): SimLayerStats {
    this.pool.begin();
    this.pool.end();
    this.ring.visible = false;
    this.label.visible = false;
    return { visible: 0, total: 0, localVisible: false };
  }

  destroy(): void {
    this.pool.destroy();
    this.ring.destroy();
    this.label.destroy();
  }
}

export { DEAD_COLOR, PLAYER_COLOR };
