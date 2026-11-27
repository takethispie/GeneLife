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
 * Genos sprite width/height as a fraction of one tile.
 *
 * This used to be `0.9`, i.e. a ~29px sprite on a 32px tile at the default
 * zoom of 32. At stress density (~2 genoses per tile) the sprites alone tiled
 * over the ground and buried the roads and buildings. The PO reviewed the
 * halved value (0.45) and chose this smaller one: 0.35 tiles, an ~11px sprite
 * at the default zoom, which leaves clear ground between genoses at density.
 */
const GENOS_SPRITE_SIZE_TILES = 0.35;

/**
 * Maximum number of nameplates drawn in one frame, local genos included.
 *
 * A label per genos was the second half of the density bug: at zoom 32 the
 * (screen-pixel sized) `Text` stayed legible while sprites shrank, so a dense
 * map became solid rows of names. Only genoses near the local player are
 * labelled (see `GENOS_LABEL_RADIUS_TILES`) and never more than this many.
 */
const MAX_GENOS_LABELS = 24;

/**
 * Radius, in tiles, around the anchor genos inside which a nameplate may be
 * drawn. Outside it the sprite alone identifies the genos; at the default zoom
 * this is a ~384px circle in the middle of a 1280x720 viewport.
 */
const GENOS_LABEL_RADIUS_TILES = 12;

/**
 * Minimum on-screen distance between two nameplates, in screen pixels.
 *
 * Nameplates are drawn at a fixed screen size (`scale = 1 / zoom`) while the map
 * underneath them zooms, so the gap that keeps them legible is a screen-space
 * one: a fontSize-10 label is ~55px wide, so 64px between anchors stops two
 * neighbour names from merging. The densest fixture packs ~2 genoses into every
 * tile, and without this the nearest N within the radius stack onto the same few
 * pixels and merge back into exactly the solid rows of text the cap exists to
 * prevent.
 */
const GENOS_LABEL_MIN_SEPARATION_PX = 64;

/**
 * Zoom (pixels per tile) at or above which genos nameplates are fully opaque.
 * The default zoom of 32 sits above this, so the first impression stays labelled.
 */
const LABEL_FADE_FULL_ZOOM = 24;

/**
 * Zoom at or below which genos nameplates have faded out completely. At the
 * camera floor of 12 names are gone, so a far-out map reads as landscape rather
 * than a wall of text.
 */
const LABEL_FADE_NONE_ZOOM = 14;

/**
 * Nameplate alpha for a camera zoom, linearly interpolated between the fully
 * opaque and fully transparent zoom edges. Values outside the range clamp to
 * 1 (zoomed in) or 0 (zoomed out).
 */
function labelAlphaForZoom(zoom: number): number {
  const span = LABEL_FADE_FULL_ZOOM - LABEL_FADE_NONE_ZOOM;
  if (span <= 0) return zoom >= LABEL_FADE_FULL_ZOOM ? 1 : 0;
  const t = (zoom - LABEL_FADE_NONE_ZOOM) / span;
  return Math.min(1, Math.max(0, t));
}

/**
 * Renders interpolated genoses as pooled layered appearance sprites. The same
 * descriptor hash composes the same layers; position and scale are updated per
 * frame without reallocating textures.
 *
 * Nameplates are rationed: only genoses within `GENOS_LABEL_RADIUS_TILES` of the
 * anchor (the local genos when it is on screen, otherwise the camera centre) are
 * eligible, and at most `MAX_GENOS_LABELS` are drawn — nearest first. The local
 * genos keeps its own distinct label (its own colour) and the ring, and falls
 * back to "You"; it is never dropped by the cap. Eligible labels come from a
 * `DisplayPool` so panning reuses the same `Text` objects instead of allocating
 * one per genos per frame.
 */
export class GenosLayer {
  private readonly appearance: AppearanceSpritePool;
  private readonly ring: Sprite;
  private readonly localLabel: Text;
  private readonly labelPool: DisplayPool<Text>;

  /**
   * Per-frame scratch buffers for label selection, kept as fields so a dense
   * frame does not allocate a fresh candidate list every time.
   */
  private readonly candidates: RenderGenos[] = [];
  private readonly candidateNames: string[] = [];
  private readonly candidateDistSq: number[] = [];
  private readonly candidateOrder: number[] = [];
  private readonly acceptedOrder: number[] = [];

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
    const baseSize = tileSize * GENOS_SPRITE_SIZE_TILES;
    const labelScale = 1 / camera.zoom;
    const labelAlpha = labelAlphaForZoom(camera.zoom);
    // Names fade out as the camera pulls back; at zero alpha no label is drawn
    // and none is counted, so the selection work is skipped entirely.
    const labelsEnabled = labelAlpha > 0;
    let visible = 0;
    let localVisible = false;
    let local: RenderGenos | null = null;
    this.candidates.length = 0;
    this.candidateNames.length = 0;
    this.candidateDistSq.length = 0;

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

      // A dead genos is a corpse: it keeps its sprite but must not stay
      // labelled. An unnamed genos has nothing to show either. Both are dropped
      // here so they never take one of the label slots.
      if (!labelsEnabled || genos.isDead === true) continue;
      const name = nameOf(genos.networkId);
      if (name === "") continue;

      this.candidates.push(genos);
      this.candidateNames.push(name);
    }

    // Labelling is anchored on the local genos when it is on screen — "close to
    // the local player" — and falls back to the camera centre so panning away
    // from your own genos does not blank every name in the viewport.
    const anchorX = local?.x ?? camera.center.x;
    const anchorY = local?.y ?? camera.center.y;
    const radius = tileSize * GENOS_LABEL_RADIUS_TILES;
    const radiusSq = radius * radius;

    // The nearest eligible genoses are labelled, capped by MAX_GENOS_LABELS.
    // The local genos' own label is never subject to the cap, so it reserves a
    // slot of its own — and, since it always sits on the anchor, nothing else is
    // labelled inside GENOS_LABEL_MIN_SEPARATION_TILES of it either.
    const localWillLabel = local !== null && local.isDead !== true && labelsEnabled;
    const maxLabels = Math.max(0, MAX_GENOS_LABELS - (localWillLabel ? 1 : 0));
    const separationSq = (GENOS_LABEL_MIN_SEPARATION_PX / camera.zoom) ** 2;
    let nearestCount = 0;
    for (let i = 0; i < this.candidates.length; i++) {
      const genos = this.candidates[i]!;
      const dx = genos.x - anchorX;
      const dy = genos.y - anchorY;
      const distSq = dx * dx + dy * dy;
      if (distSq > radiusSq) continue;
      if (localWillLabel && distSq < separationSq) continue;
      this.candidates[nearestCount] = genos;
      this.candidateNames[nearestCount] = this.candidateNames[i]!;
      this.candidateDistSq[nearestCount] = distSq;
      nearestCount++;
    }
    this.candidates.length = nearestCount;
    this.candidateNames.length = nearestCount;
    this.candidateDistSq.length = nearestCount;

    // Nearest first, then a greedy declutter pass: a nameplate is drawn only
    // when it is at least GENOS_LABEL_MIN_SEPARATION_TILES away from every
    // nameplate accepted before it. At stress density two genoses share a tile,
    // so the nearest N would otherwise pile onto the same few pixels and merge
    // back into the solid rows of text the cap exists to prevent.
    const accepted = this.acceptedOrder;
    accepted.length = 0;
    if (nearestCount > 0) {
      this.candidateOrder.length = nearestCount;
      for (let i = 0; i < nearestCount; i++) this.candidateOrder[i] = i;
      this.candidateOrder.sort(
        (a, b) => this.candidateDistSq[a]! - this.candidateDistSq[b]!
      );
      for (let i = 0; i < nearestCount && accepted.length < maxLabels; i++) {
        const index = this.candidateOrder[i]!;
        const genos = this.candidates[index]!;
        let clear = true;
        for (let j = 0; j < accepted.length; j++) {
          const other = this.candidates[accepted[j]!]!;
          const dx = genos.x - other.x;
          const dy = genos.y - other.y;
          if (dx * dx + dy * dy < separationSq) {
            clear = false;
            break;
          }
        }
        if (clear) accepted.push(index);
      }
    }

    let labels = 0;
    for (let i = 0; i < accepted.length; i++) {
      const index = accepted[i]!;
      const genos = this.candidates[index]!;
      const name = this.candidateNames[index]!;
      const label = this.labelPool.acquire();
      if (label.text !== name) label.text = name;
      label.position.set(genos.x, genos.y - tileSize * LABEL_OFFSET_TILES);
      label.scale.set(labelScale);
      label.alpha = labelAlpha;
      labels++;
    }
    this.candidateOrder.length = 0;
    accepted.length = 0;

    this.appearance.end();
    this.labelPool.end();

    if (local !== null) {
      this.ring.visible = true;
      this.ring.position.set(local.x, local.y);
      this.ring.width = tileSize * 1.45;
      this.ring.height = tileSize * 1.45;

      // The ring is not a name, so a dead or far-zoomed local genos keeps it
      // while its own nameplate is suppressed.
      if (local.isDead === true || labelAlpha <= 0) {
        this.localLabel.visible = false;
      } else {
        this.localLabel.visible = true;
        const localName = nameOf(local.networkId);
        const text = localName === "" ? "You" : localName;
        if (this.localLabel.text !== text) this.localLabel.text = text;
        this.localLabel.position.set(local.x, local.y - tileSize * LABEL_OFFSET_TILES);
        this.localLabel.scale.set(labelScale);
        this.localLabel.alpha = labelAlpha;
        labels++;
      }
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
export {
  GENOS_LABEL_MIN_SEPARATION_PX,
  GENOS_LABEL_RADIUS_TILES,
  GENOS_SPRITE_SIZE_TILES,
  MAX_GENOS_LABELS
};
