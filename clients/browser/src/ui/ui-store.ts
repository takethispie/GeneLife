import type { MapBuilding } from "../contracts/hub";
import type { ConnectionState } from "../net/events";

export type Selection =
  | { kind: "building"; building: MapBuilding }
  | { kind: "tile"; x: number; y: number }
  | null;

export interface HudState {
  connection: ConnectionState;
  playerId: string;
  ownSimNetworkId: number;
  mapId: string;
  mapVersion: number;
  tick: number;
  fps: number;
  frameTimeMs: number;
  visibleSims: number;
  totalSims: number;
  localPlayerVisible: boolean;
  viewportRadius: number;
  interpolationDelayMs: number;
  inputAckP50Ms: number;
  inputAckP95Ms: number;
  bufferFrames: number;
  reconnectCount: number;
  lastError: string;
  selection: Selection;
  buildStamp: string;
  protocolVersion: number;
  cameraCenterX: number;
  cameraCenterY: number;
  cameraZoom: number;
  renderer: string;
  bytesPerSecond: number;
}

export const INITIAL_HUD_STATE: HudState = {
  connection: "idle",
  playerId: "",
  ownSimNetworkId: 0,
  mapId: "",
  mapVersion: 0,
  tick: 0,
  fps: 0,
  frameTimeMs: 0,
  visibleSims: 0,
  totalSims: 0,
  localPlayerVisible: false,
  viewportRadius: 0,
  interpolationDelayMs: 0,
  inputAckP50Ms: 0,
  inputAckP95Ms: 0,
  bufferFrames: 0,
  reconnectCount: 0,
  lastError: "",
  selection: null,
  buildStamp: "dev",
  protocolVersion: 1,
  cameraCenterX: 0,
  cameraCenterY: 0,
  cameraZoom: 1,
  renderer: "",
  bytesPerSecond: 0
};

/**
 * Tiny observable for low-frequency HUD/session state. It deliberately does not
 * hold sim positions; Pixi reads the simulation store directly.
 */
export class UiStore {
  private state: HudState = { ...INITIAL_HUD_STATE };
  private readonly listeners = new Set<(state: HudState) => void>();

  getState(): HudState {
    return this.state;
  }

  patch(partial: Partial<HudState>): void {
    let changed = false;
    for (const key of Object.keys(partial) as (keyof HudState)[]) {
      if (!Object.is(this.state[key], partial[key])) {
        changed = true;
        break;
      }
    }
    if (!changed) return;

    this.state = { ...this.state, ...partial };
    for (const listener of this.listeners) listener(this.state);
  }

  subscribe(listener: (state: HudState) => void): () => void {
    this.listeners.add(listener);
    listener(this.state);
    return () => this.listeners.delete(listener);
  }

  reset(): void {
    this.patch({ ...INITIAL_HUD_STATE });
  }
}
