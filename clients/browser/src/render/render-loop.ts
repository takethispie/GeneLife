import type { MapBuilding } from "../contracts/hub";
import type { TileRect } from "../net/viewport-client";
import type { SimulationStore, RenderState } from "../simulation/store";
import type { ClientSettings } from "../ui/settings";
import { Camera } from "./camera";
import type { DebugMetrics } from "./debug-overlay";
import type { PixiScene } from "./pixi-app";
import { TileLayer } from "./map/tile-layer";
import { BuildingLayer } from "./map/building-layer";
import { GenosLayer } from "./genos-layer";
import { SelectionLayer } from "./selection-layer";
import { createSceneTextures, destroyTextures, type SceneTextures } from "./textures";
import type { RenderGenos } from "../simulation/interpolation";

export interface SceneStats extends DebugMetrics {
  visibleRoads: number;
  visibleEntryMarkers: number;
  labelCount: number;
  genosLabels: number;
  localPlayerVisible: boolean;
}

export interface SceneRendererOptions {
  pixi: PixiScene;
  store: SimulationStore;
  camera: Camera;
  getSettings: () => ClientSettings;
  onStats: (stats: SceneStats) => void;
  onViewportRect: (rect: TileRect) => void;
  onMapChanged: () => void;
}

/**
 * Frame-oriented renderer. It reads the simulation store, never mutates network
 * state, and creates Pixi objects only from the render loop.
 */
export class SceneRenderer {
  private textures: SceneTextures | null = null;
  private tiles: TileLayer | null = null;
  private buildings: BuildingLayer | null = null;
  private genoses: GenosLayer | null = null;
  private selection: SelectionLayer | null = null;
  private lastMapKey = "";
  private lastScreenWidth = -1;
  private lastScreenHeight = -1;
  private tickIntervalMs = 40;
  private interpolationDelayMs = 120;
  private frameAverageMs = 16.7;
  private lastStatsAt = 0;
  private selectedBuilding: MapBuilding | null = null;
  private hoverTile: { x: number; y: number; walkable: boolean } | null = null;
  private targetTile: { x: number; y: number } | null = null;
  /** The interpolated positions from the most recent frame, for pointer picking. */
  private lastRenderState: RenderState | null = null;
  private readonly options: SceneRendererOptions;
  /** Stable lookup so the genos layer reads names without a per-frame closure. */
  private readonly nameOf = (networkId: number): string =>
    this.options.store.genoses.get(networkId)?.name ?? "";

  constructor(options: SceneRendererOptions) {
    this.options = options;
  }

  init(): void {
    const { pixi } = this.options;
    if (pixi.layers === null) throw new Error("Pixi scene is not initialized");
    this.textures = createSceneTextures(pixi.app.renderer);
    this.tiles = new TileLayer(pixi.layers.terrain, pixi.layers.roads, this.textures.white);
    this.buildings = new BuildingLayer(pixi.layers.buildings, pixi.layers.effects, this.textures);
    this.genoses = new GenosLayer(pixi.layers.genoses, this.textures);
    this.selection = new SelectionLayer(pixi.layers.selection);
    pixi.onFrame((deltaMs) => this.frame(deltaMs));
  }

  setTickInterval(ms: number): void {
    if (Number.isFinite(ms) && ms > 0) this.tickIntervalMs = ms;
  }

  setInterpolationDelay(ms: number): void {
    if (Number.isFinite(ms) && ms >= 0) this.interpolationDelayMs = ms;
  }

  setHover(tile: { x: number; y: number } | null, walkable: boolean): void {
    this.hoverTile = tile === null ? null : { x: tile.x, y: tile.y, walkable };
  }

  setTarget(tile: { x: number; y: number } | null): void {
    this.targetTile = tile === null ? null : { x: tile.x, y: tile.y };
  }

  setSelectedBuilding(building: MapBuilding | null): void {
    this.selectedBuilding = building;
  }

  /**
   * The interpolated genos positions drawn in the last frame. Pointer picking
   * reads these so a click lands on the same place the sprite is drawn, not on
   * the older snapshot.
   */
  renderGenoses(): Iterable<RenderGenos> {
    return this.lastRenderState?.genoses.values() ?? [];
  }

  dispose(): void {
    this.tiles?.destroy();
    this.buildings?.destroy();
    this.genoses?.destroy();
    this.selection?.destroy();
    if (this.textures !== null) destroyTextures(this.textures);
    this.tiles = null;
    this.buildings = null;
    this.genoses = null;
    this.selection = null;
    this.textures = null;
  }

  private syncCameraViewport(): void {
    const width = this.options.pixi.screenWidth;
    const height = this.options.pixi.screenHeight;
    if (width === this.lastScreenWidth && height === this.lastScreenHeight) return;
    this.lastScreenWidth = width;
    this.lastScreenHeight = height;
    this.options.camera.resize(width, height);
  }

  private frame(deltaMs: number): void {
    const { pixi, store, camera } = this.options;
    const settings = this.options.getSettings();
    const frames = Math.max(1, deltaMs);
    this.frameAverageMs = this.frameAverageMs * 0.9 + frames * 0.1;
    this.syncCameraViewport();
    camera.update(deltaMs / 1000);
    pixi.applyCamera(camera.containerX(), camera.containerY(), camera.zoom);

    const map = store.map;
    if (map === null) {
      this.lastRenderState = null;
      this.genoses?.renderNoWorld();
      this.lastStatsAt = 0;
      return;
    }

    const mapKey = `${map.mapId}:${map.version}`;
    if (mapKey !== this.lastMapKey) {
      this.lastMapKey = mapKey;
      camera.setMap(map);
      this.options.onMapChanged();
    }

    const renderState = store.getRenderState(performance.now(), {
      tickIntervalMs: this.tickIntervalMs,
      interpolationDelayMs: settings.reduceMotion ? 0 : this.interpolationDelayMs,
      snapDistance: 3,
      reducedMotion: settings.reduceMotion
    });
    this.lastRenderState = renderState;

    const tileStats = this.tiles?.render(map, camera) ?? { tiles: 0, roads: 0 };
    const buildingStats = this.buildings?.render(map, camera) ?? {
      buildings: 0,
      entries: 0,
      labels: 0
    };
    const ownGenos = store.ownGenosId;
    const genosStats = this.genoses?.render(renderState, ownGenos, this.nameOf, camera, map.tileSize) ?? {
      visible: 0,
      total: 0,
      labels: 0,
      localVisible: false
    };

    this.selection?.setHover(map, this.hoverTile, this.hoverTile?.walkable ?? false);
    this.selection?.setTarget(map, this.targetTile);
    this.selection?.setBuilding(map, this.selectedBuilding);

    const rect = camera.visibleTileRect(map);
    this.options.onViewportRect(rect);

    const now = performance.now();
    if (now - this.lastStatsAt >= 250) {
      this.lastStatsAt = now;
      const stats: SceneStats = {
        fps: 1000 / Math.max(1, this.frameAverageMs),
        frameTimeMs: this.frameAverageMs,
        mapId: map.mapId,
        mapVersion: map.version,
        cameraX: camera.center.x,
        cameraY: camera.center.y,
        zoom: camera.zoom,
        visibleTiles: tileStats.tiles,
        visibleRoads: tileStats.roads,
        visibleBuildings: buildingStats.buildings,
        visibleEntryMarkers: buildingStats.entries,
        labelCount: buildingStats.labels,
        genosLabels: genosStats.labels,
        visibleGenoses: genosStats.visible,
        totalGenoses: genosStats.total,
        localPlayerVisible: genosStats.localVisible,
        tick: renderState?.tick ?? store.tick,
        bufferFrames: renderState?.frameCount ?? 0,
        interpolationDelayMs: renderState?.interpolationDelayMs ?? 0,
        inputAckP50Ms: 0,
        inputAckP95Ms: 0,
        bytesPerSecond: 0,
        reconnectCount: 0,
        renderer: pixi.rendererName,
        buildStamp: "dev"
      };
      this.options.onStats(stats);
    }
  }
}
