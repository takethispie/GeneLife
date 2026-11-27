import type { MapBuilding } from "../contracts/hub";
import type { IntentClient } from "../net/intent-client";
import type { Camera } from "../render/camera";
import {
  buildingAt,
  inBounds,
  isWalkableHint,
  type DecodedMap
} from "../simulation/map/rle-decoder";
import type { NotificationKind } from "../ui/notifications";

export interface TilePoint {
  x: number;
  y: number;
}

/**
 * Radius, in tile units, within which a left-click is treated as picking a
 * genos. Genos sprites are deliberately small since #99
 * (`GENOS_SPRITE_SIZE_TILES = 0.35`, ~11px at the default zoom), so the pick
 * radius must be forgiving relative to the *drawn* sprite or clicking one would
 * feel broken. 0.45 tiles is comfortably larger than the rendered sprite and
 * smaller than a tile, so a click still reads as "on that genos".
 */
export const GENOS_PICK_RADIUS_TILES = 0.45;

/** A candidate for pointer picking: a network id and its current world position. */
export interface GenosPickTarget {
  networkId: number;
  x: number;
  y: number;
}

/**
 * Nearest genos to a world point within `radiusWorld`, or null. Pure so picking
 * is unit-testable without a Pixi scene: it only needs the interpolated render
 * positions the caller already has.
 */
export function nearestGenos(
  world: { x: number; y: number },
  genoses: Iterable<GenosPickTarget>,
  radiusWorld: number
): number | null {
  let bestId: number | null = null;
  let bestDistSq = radiusWorld * radiusWorld;
  for (const genos of genoses) {
    const dx = genos.x - world.x;
    const dy = genos.y - world.y;
    const distSq = dx * dx + dy * dy;
    if (distSq <= bestDistSq) {
      bestDistSq = distSq;
      bestId = genos.networkId;
    }
  }
  return bestId;
}

export interface TargetingOptions {
  canvas: HTMLCanvasElement;
  camera: Camera;
  getMap: () => DecodedMap | null;
  intent: IntentClient;
  onHover: (tile: TilePoint | null, walkable: boolean) => void;
  onTarget: (tile: TilePoint | null) => void;
  onSelectBuilding: (building: MapBuilding | null) => void;
  /** Current interpolated render positions to pick genoses from. */
  getGenoses?: () => Iterable<GenosPickTarget>;
  /** Called with a picked genos' network id, or null when the selection clears. */
  onSelectGenos?: (networkId: number | null) => void;
  /** Called when the player issues a manual move, so pending actions can cancel. */
  onPlayerMove?: () => void;
  notify: (kind: NotificationKind, code: string, message: string) => void;
}

/**
 * Translates pointer/keyboard targets into server intents.
 *
 * The right mouse button is the click-to-move gesture. The left button only
 * selects (buildings and units) and drives the UI; it never issues a move.
 * Touch and pen have no right button, so a tap is the primary action — it
 * selects a building when one is under the pointer and otherwise moves.
 *
 * Hover and target markers are presentation only; the client never applies a
 * position locally.
 */
export class TargetingController {
  private hover: { tile: TilePoint; walkable: boolean } | null = null;
  private disposed = false;

  private readonly onPointerMove = (event: PointerEvent): void => {
    if (isHudTarget(event.target)) return;
    const tile = this.tileFromEvent(event);
    if (tile === null) {
      this.setHover(null);
      return;
    }
    const map = this.options.getMap();
    if (map === null) return;
    const walkable = isWalkableHint(map, tile.x, tile.y) && buildingAt(map, tile.x, tile.y) === null;
    this.setHover({ tile, walkable });
  };

  private readonly onPointerLeave = (): void => this.setHover(null);

  /** Right mouse button: the click-to-move gesture. */
  private readonly onPointerDown = (event: PointerEvent): void => {
    if (isHudTarget(event.target)) return;
    if (event.button !== 2) return;
    this.moveAtEvent(event);
  };

  /** Left click only selects; it never issues a move. */
  private readonly onClick = (event: MouseEvent): void => {
    if (isHudTarget(event.target)) return;
    if (event.button !== 0) return;
    // Alt+left is a camera pan gesture, not a target.
    if (event.altKey) return;
    this.selectAtEvent(event);
  };

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (event.key === "Enter" && (document.activeElement === this.options.canvas || document.activeElement === document.body)) {
      event.preventDefault();
      if (this.hover !== null) this.activate(this.hover.tile);
    }
  };

  constructor(private readonly options: TargetingOptions) {
    options.canvas.addEventListener("pointermove", this.onPointerMove);
    options.canvas.addEventListener("pointerleave", this.onPointerLeave);
    options.canvas.addEventListener("pointerdown", this.onPointerDown);
    options.canvas.addEventListener("click", this.onClick);
    window.addEventListener("keydown", this.onKeyDown);
  }

  clearTarget(): void {
    this.options.onTarget(null);
  }

  clearAll(): void {
    this.clearTarget();
    this.options.onSelectBuilding(null);
    this.options.onSelectGenos?.(null);
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.options.canvas.removeEventListener("pointermove", this.onPointerMove);
    this.options.canvas.removeEventListener("pointerleave", this.onPointerLeave);
    this.options.canvas.removeEventListener("pointerdown", this.onPointerDown);
    this.options.canvas.removeEventListener("click", this.onClick);
    window.removeEventListener("keydown", this.onKeyDown);
  }

  /**
   * Touch/pen entry point. A tap has no right button, so it performs the
   * primary action: select a building under the pointer, otherwise move there.
   */
  activateAtScreenPoint(clientX: number, clientY: number): void {
    const tile = this.tileFromPoint(clientX, clientY);
    if (tile === null) {
      this.options.notify("info", "outside_map", "That is outside the map");
      return;
    }
    this.activate(tile);
  }

  /** Left click: select the genos or building under the pointer, else clear. */
  private selectAtEvent(event: MouseEvent): void {
    const map = this.options.getMap();
    if (map === null) return;
    const tile = this.tileFromEvent(event);
    if (tile === null || !inBounds(map, tile.x, tile.y)) {
      this.options.notify("info", "outside_map", "That is outside the map");
      return;
    }

    // A genos under the pointer wins over the tile's building: you clicked the
    // unit, not the ground it stands on.
    const genosId = this.pickGenosAtEvent(event);
    if (genosId !== null) {
      this.options.onTarget(null);
      this.options.onSelectBuilding(null);
      this.options.onSelectGenos?.(genosId);
      return;
    }

    const building = buildingAt(map, tile.x, tile.y);
    if (building !== null) {
      this.options.onTarget(null);
      this.options.onSelectGenos?.(null);
      this.options.onSelectBuilding(building);
      return;
    }

    // Empty ground: clear whatever was selected.
    this.options.onSelectGenos?.(null);
    this.options.onSelectBuilding(null);
  }

  /** Right click: move to the tile. */
  private moveAtEvent(event: MouseEvent): void {
    const tile = this.tileFromEvent(event);
    if (tile === null) {
      this.options.notify("info", "outside_map", "That is outside the map");
      return;
    }
    this.move(tile);
  }

  /** Primary action (touch tap, Enter): select a genos or building, otherwise move. */
  private activate(tile: TilePoint): void {
    const map = this.options.getMap();
    if (map === null) return;
    const genosId = this.pickGenosAtTile(tile);
    if (genosId !== null) {
      this.options.onTarget(null);
      this.options.onSelectBuilding(null);
      this.options.onSelectGenos?.(genosId);
      return;
    }
    const building = buildingAt(map, tile.x, tile.y);
    if (building !== null) {
      this.options.onTarget(null);
      this.options.onSelectGenos?.(null);
      this.options.onSelectBuilding(building);
      return;
    }
    this.move(tile);
  }

  private move(tile: TilePoint): void {
    const map = this.options.getMap();
    if (map === null) return;

    if (!inBounds(map, tile.x, tile.y)) {
      this.options.notify("info", "outside_map", "That is outside the map");
      return;
    }

    // Buildings and occupied/water tiles are not move targets; mirror the
    // server, which would reject them as blocked.
    if (!isWalkableHint(map, tile.x, tile.y) || buildingAt(map, tile.x, tile.y) !== null) {
      this.options.onTarget(null);
      this.options.notify("warning", "blocked_tile", "That tile is blocked");
      return;
    }

    this.options.onTarget(tile);
    this.options.onPlayerMove?.();
    void this.submitMove(tile);
  }

  private async submitMove(tile: TilePoint): Promise<void> {
    const outcome = await this.options.intent.moveTo(tile.x, tile.y);
    if (outcome.status === "rate-limited") {
      this.options.notify("warning", "intent_rate_limited", outcome.message);
    } else if (outcome.status === "not-ready") {
      this.options.notify("warning", "intent_not_ready", outcome.message);
    } else if (outcome.status === "failed") {
      this.options.notify("error", "intent_rejected", outcome.message);
    }
  }

  private tileFromEvent(event: MouseEvent | PointerEvent): TilePoint | null {
    return this.tileFromPoint(event.clientX, event.clientY);
  }

  private tileFromPoint(clientX: number, clientY: number): TilePoint | null {
    const map = this.options.getMap();
    if (map === null) return null;
    const rect = this.options.canvas.getBoundingClientRect();
    return this.options.camera.tileFromScreen(clientX - rect.left, clientY - rect.top);
  }

  /** Picks the genos under a mouse event, or null. */
  private pickGenosAtEvent(event: MouseEvent): number | null {
    const rect = this.options.canvas.getBoundingClientRect();
    const world = this.options.camera.screenToWorld(event.clientX - rect.left, event.clientY - rect.top);
    return this.pickGenosAtWorld(world);
  }

  /** Picks the genos under a tile (touch tap / Enter), or null. */
  private pickGenosAtTile(tile: TilePoint): number | null {
    const tileSize = this.options.camera.tileSize;
    return this.pickGenosAtWorld({ x: tile.x * tileSize, y: tile.y * tileSize });
  }

  private pickGenosAtWorld(world: { x: number; y: number }): number | null {
    const getGenoses = this.options.getGenoses;
    if (getGenoses === undefined) return null;
    const radius = GENOS_PICK_RADIUS_TILES * this.options.camera.tileSize;
    return nearestGenos(world, getGenoses(), radius);
  }

  private setHover(next: { tile: TilePoint; walkable: boolean } | null): void {
    const changed =
      this.hover === null ||
      next === null ||
      this.hover.tile.x !== next.tile.x ||
      this.hover.tile.y !== next.tile.y ||
      this.hover.walkable !== next.walkable;
    if (!changed) return;
    this.hover = next;
    if (next === null) this.options.onHover(null, false);
    else this.options.onHover({ x: next.tile.x, y: next.tile.y }, next.walkable);
  }
}

function isHudTarget(target: EventTarget | null): boolean {
  return target instanceof Element && target.closest("#hud") !== null;
}
