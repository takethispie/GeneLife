import { Application, Container } from "pixi.js";

export interface SceneLayers {
  terrain: Container;
  roads: Container;
  buildings: Container;
  genoses: Container;
  selection: Container;
  effects: Container;
}

export function createLayers(world: Container): SceneLayers {
  const layers: SceneLayers = {
    terrain: new Container(),
    roads: new Container(),
    buildings: new Container(),
    genoses: new Container(),
    selection: new Container(),
    effects: new Container()
  };
  layers.terrain.label = "terrain";
  layers.roads.label = "roads";
  layers.buildings.label = "buildings";
  layers.genoses.label = "genoses";
  layers.selection.label = "selection";
  layers.effects.label = "effects";
  for (const layer of Object.values(layers)) world.addChild(layer);
  return layers;
}

export interface PixiSceneOptions {
  resolutionCap?: number;
  background?: string;
}

export type FrameCallback = (deltaMs: number) => void;

/**
 * One Pixi application, one world container and one ticker. The camera
 * transform is applied to `world`; layers never move independently.
 */
export class PixiScene {
  readonly app = new Application();
  readonly world = new Container();
  layers: SceneLayers | null = null;
  private readonly frameCallbacks = new Set<FrameCallback>();
  private tickerHandler: ((ticker: { deltaMS: number }) => void) | null = null;
  private visibilityHandler: (() => void) | null = null;
  private initialized = false;

  constructor(private readonly options: PixiSceneOptions = {}) {}

  async init(parent: HTMLElement): Promise<void> {
    const resolutionCap = this.options.resolutionCap ?? 2;
    await this.app.init({
      background: this.options.background ?? "#101418",
      resizeTo: window,
      antialias: false,
      resolution: Math.min(window.devicePixelRatio || 1, resolutionCap),
      autoDensity: true,
      preference: "webgl"
    });

    this.app.canvas.setAttribute("aria-label", "GeneLife world map");
    this.app.canvas.setAttribute("role", "application");
    this.app.canvas.tabIndex = 0;
    parent.appendChild(this.app.canvas);

    this.world.label = "world";
    this.app.stage.addChild(this.world);
    this.layers = createLayers(this.world);

    this.visibilityHandler = () => {
      if (document.hidden) {
        this.app.ticker.stop();
      } else {
        this.app.ticker.start();
      }
    };
    document.addEventListener("visibilitychange", this.visibilityHandler);
    this.initialized = true;
  }

  get screenWidth(): number {
    return this.app.screen.width;
  }

  get screenHeight(): number {
    return this.app.screen.height;
  }

  get rendererName(): string {
    try {
      return this.app.renderer.name;
    } catch {
      return "unknown";
    }
  }

  onFrame(callback: FrameCallback): () => void {
    return this.addFrameCallback(callback);
  }

  addFrameCallback(callback: FrameCallback): () => void {
    this.frameCallbacks.add(callback);
    if (this.tickerHandler === null) {
      this.tickerHandler = (ticker) => {
        for (const frameCallback of this.frameCallbacks) frameCallback(ticker.deltaMS);
      };
      this.app.ticker.add(this.tickerHandler);
    }
    return () => this.frameCallbacks.delete(callback);
  }

  applyCamera(x: number, y: number, zoom: number): void {
    this.world.position.set(x, y);
    this.world.scale.set(zoom);
  }

  pause(): void {
    this.app.ticker.stop();
  }

  resume(): void {
    this.app.ticker.start();
  }

  destroy(): void {
    if (this.tickerHandler !== null) {
      this.app.ticker.remove(this.tickerHandler);
      this.tickerHandler = null;
    }
    this.frameCallbacks.clear();
    if (this.visibilityHandler !== null) {
      document.removeEventListener("visibilitychange", this.visibilityHandler);
      this.visibilityHandler = null;
    }
    if (this.initialized) {
      this.initialized = false;
      this.app.destroy(true, { children: true, texture: false });
    }
  }
}
