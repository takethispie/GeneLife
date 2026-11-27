import type { TileRect } from "../net/viewport-client";
import type { DecodedMap } from "../simulation/map/rle-decoder";

export interface CameraOptions {
  minZoom?: number;
  maxZoom?: number;
  reducedMotion?: boolean;
}

/**
 * Camera in world units with a single Pixi container transform. World
 * coordinates equal tile coordinates multiplied by the map's tile size.
 */
export class Camera {
  private centerX = 0;
  private centerY = 0;
  private targetX = 0;
  private targetY = 0;
  private zoomValue = 32;
  private screenWidth = 1;
  private screenHeight = 1;
  private minZoom: number;
  private maxZoom: number;
  private reducedMotion: boolean;
  private bounds: { minX: number; minY: number; maxX: number; maxY: number } | null = null;
  private tileSizeValue = 1;

  constructor(options: CameraOptions = {}) {
    this.minZoom = options.minZoom ?? 12;
    this.maxZoom = options.maxZoom ?? 128;
    this.reducedMotion = options.reducedMotion ?? false;
  }

  get center(): { x: number; y: number } {
    return { x: this.centerX, y: this.centerY };
  }

  get zoom(): number {
    return this.zoomValue;
  }

  /** World-unit size of one tile (set from the decoded map). */
  get tileSize(): number {
    return this.tileSizeValue;
  }

  setReducedMotion(value: boolean): void {
    this.reducedMotion = value;
  }

  resize(width: number, height: number): void {
    this.screenWidth = Math.max(1, width);
    this.screenHeight = Math.max(1, height);
    this.clampTarget();
    if (this.reducedMotion) this.snapToTarget();
  }

  setMap(map: DecodedMap): void {
    this.tileSizeValue = map.tileSize;
    this.bounds = {
      minX: (map.minX - 0.5) * map.tileSize,
      minY: (map.minY - 0.5) * map.tileSize,
      maxX: (map.maxX + 0.5) * map.tileSize,
      maxY: (map.maxY + 0.5) * map.tileSize
    };
    this.minZoom = Math.min(this.minZoom, this.fitZoom());
    this.clampTarget();
    this.snapToTarget();
  }

  centerOnTile(tileX: number, tileY: number): void {
    this.centerX = tileX * this.tileSizeValue;
    this.centerY = tileY * this.tileSizeValue;
    this.targetX = this.centerX;
    this.targetY = this.centerY;
    this.clampTarget();
  }

  centerOnWorld(x: number, y: number): void {
    this.targetX = x;
    this.targetY = y;
    this.clampTarget();
    if (this.reducedMotion) this.snapToTarget();
  }

  panByTiles(deltaX: number, deltaY: number): void {
    this.targetX += deltaX * this.tileSizeValue;
    this.targetY += deltaY * this.tileSizeValue;
    this.clampTarget();
    if (this.reducedMotion) this.snapToTarget();
  }

  setZoom(zoom: number): void {
    this.zoomValue = Math.min(this.maxZoom, Math.max(this.minZoom, zoom));
    this.clampTarget();
    if (this.reducedMotion) this.snapToTarget();
  }

  /** Zooms while keeping the world point under the pointer fixed. */
  zoomAt(factor: number, screenX: number, screenY: number): void {
    const world = this.screenToWorld(screenX, screenY);
    this.setZoom(this.zoomValue * factor);
    this.targetX = world.x - (screenX - this.screenWidth / 2) / this.zoomValue;
    this.targetY = world.y - (screenY - this.screenHeight / 2) / this.zoomValue;
    this.clampTarget();
    if (this.reducedMotion) this.snapToTarget();
  }

  update(deltaSeconds: number): void {
    if (this.reducedMotion) {
      this.snapToTarget();
      return;
    }
    const t = 1 - Math.exp(-Math.max(0, deltaSeconds) * 14);
    this.centerX += (this.targetX - this.centerX) * t;
    this.centerY += (this.targetY - this.centerY) * t;
  }

  snapToTarget(): void {
    this.centerX = this.targetX;
    this.centerY = this.targetY;
  }

  /** Applies the camera transform to the single world container. */
  containerX(): number {
    return this.screenWidth / 2 - this.centerX * this.zoomValue;
  }

  containerY(): number {
    return this.screenHeight / 2 - this.centerY * this.zoomValue;
  }

  /**
   * Screen (canvas-relative pixels) to world units. This is the single home of
   * the inverse-viewport transform; `worldFromScreen` is kept as an alias for
   * existing callers.
   */
  screenToWorld(screenX: number, screenY: number): { x: number; y: number } {
    return {
      x: (screenX - this.screenWidth / 2) / this.zoomValue + this.centerX,
      y: (screenY - this.screenHeight / 2) / this.zoomValue + this.centerY
    };
  }

  worldFromScreen(screenX: number, screenY: number): { x: number; y: number } {
    return this.screenToWorld(screenX, screenY);
  }

  tileFromScreen(screenX: number, screenY: number): { x: number; y: number } {
    const world = this.screenToWorld(screenX, screenY);
    return {
      x: Math.round(world.x / this.tileSizeValue),
      y: Math.round(world.y / this.tileSizeValue)
    };
  }

  visibleTileRect(map: DecodedMap): TileRect {
    const halfW = this.screenWidth / (2 * this.zoomValue);
    const halfH = this.screenHeight / (2 * this.zoomValue);
    const minX = Math.max(map.minX, Math.floor((this.centerX - halfW) / this.tileSizeValue));
    const maxX = Math.min(map.maxX, Math.ceil((this.centerX + halfW) / this.tileSizeValue));
    const minY = Math.max(map.minY, Math.floor((this.centerY - halfH) / this.tileSizeValue));
    const maxY = Math.min(map.maxY, Math.ceil((this.centerY + halfH) / this.tileSizeValue));
    return { minX, minY, maxX: Math.max(minX, maxX), maxY: Math.max(minY, maxY) };
  }

  isWorldVisible(x: number, y: number, margin = 1): boolean {
    if (this.screenWidth <= 0 || this.screenHeight <= 0) return false;
    const halfW = this.screenWidth / (2 * this.zoomValue) + margin;
    const halfH = this.screenHeight / (2 * this.zoomValue) + margin;
    return Math.abs(x - this.centerX) <= halfW && Math.abs(y - this.centerY) <= halfH;
  }

  private fitZoom(): number {
    if (this.bounds === null) return this.minZoom;
    const worldWidth = this.bounds.maxX - this.bounds.minX;
    const worldHeight = this.bounds.maxY - this.bounds.minY;
    return Math.min(32, this.screenWidth / Math.max(1, worldWidth), this.screenHeight / Math.max(1, worldHeight));
  }

  private clampTarget(): void {
    if (this.bounds === null) return;
    const halfW = this.screenWidth / (2 * this.zoomValue);
    const halfH = this.screenHeight / (2 * this.zoomValue);
    const mapWidth = this.bounds.maxX - this.bounds.minX;
    const mapHeight = this.bounds.maxY - this.bounds.minY;
    const centerMapX = (this.bounds.minX + this.bounds.maxX) / 2;
    const centerMapY = (this.bounds.minY + this.bounds.maxY) / 2;

    const clampX = (value: number): number =>
      mapWidth <= halfW * 2 ? centerMapX : clamp(value, this.bounds!.minX + halfW, this.bounds!.maxX - halfW);
    const clampY = (value: number): number =>
      mapHeight <= halfH * 2 ? centerMapY : clamp(value, this.bounds!.minY + halfH, this.bounds!.maxY - halfH);

    this.targetX = clampX(this.targetX);
    this.targetY = clampY(this.targetY);
    this.centerX = clampX(this.centerX);
    this.centerY = clampY(this.centerY);
  }
}

function clamp(value: number, min: number, max: number): number {
  return Math.min(max, Math.max(min, value));
}
