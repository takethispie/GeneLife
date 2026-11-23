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

export interface TargetingOptions {
  canvas: HTMLCanvasElement;
  camera: Camera;
  getMap: () => DecodedMap | null;
  intent: IntentClient;
  onHover: (tile: TilePoint | null, walkable: boolean) => void;
  onTarget: (tile: TilePoint | null) => void;
  onSelectBuilding: (building: MapBuilding | null) => void;
  notify: (kind: NotificationKind, code: string, message: string) => void;
}

/**
 * Translates pointer/keyboard targets into server intents. Hover and target
 * markers are presentation only; the client never applies a position locally.
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

  private readonly onClick = (event: MouseEvent): void => {
    if (isHudTarget(event.target)) return;
    if (event.button !== 0) return;
    // Alt+left is a camera pan gesture, not a target.
    if (event.altKey) return;
    this.activateAtEvent(event);
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
    options.canvas.addEventListener("click", this.onClick);
    window.addEventListener("keydown", this.onKeyDown);
  }

  clearTarget(): void {
    this.options.onTarget(null);
  }

  clearAll(): void {
    this.clearTarget();
    this.options.onSelectBuilding(null);
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.options.canvas.removeEventListener("pointermove", this.onPointerMove);
    this.options.canvas.removeEventListener("pointerleave", this.onPointerLeave);
    this.options.canvas.removeEventListener("click", this.onClick);
    window.removeEventListener("keydown", this.onKeyDown);
  }

  private activateAtEvent(event: MouseEvent): void {
    const tile = this.tileFromEvent(event);
    if (tile === null) {
      this.options.notify("info", "outside_map", "That is outside the map");
      return;
    }
    this.activate(tile);
  }

  private activate(tile: TilePoint): void {
    const map = this.options.getMap();
    if (map === null) return;

    if (!inBounds(map, tile.x, tile.y)) {
      this.options.notify("info", "outside_map", "That is outside the map");
      return;
    }

    const building = buildingAt(map, tile.x, tile.y);
    if (building !== null) {
      this.options.onTarget(null);
      this.options.onSelectBuilding(building);
      return;
    }

    if (!isWalkableHint(map, tile.x, tile.y)) {
      this.options.onTarget(null);
      this.options.notify("warning", "blocked_tile", "That tile is blocked");
      return;
    }

    this.options.onTarget(tile);
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
    const map = this.options.getMap();
    if (map === null) return null;
    const rect = this.options.canvas.getBoundingClientRect();
    return this.options.camera.tileFromScreen(event.clientX - rect.left, event.clientY - rect.top);
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
