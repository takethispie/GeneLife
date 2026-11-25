export interface DebugMetrics {
  fps: number;
  frameTimeMs: number;
  mapId: string;
  mapVersion: number;
  cameraX: number;
  cameraY: number;
  zoom: number;
  visibleTiles: number;
  visibleBuildings: number;
  visibleGenoses: number;
  totalGenoses: number;
  tick: number;
  bufferFrames: number;
  interpolationDelayMs: number;
  inputAckP50Ms: number;
  inputAckP95Ms: number;
  bytesPerSecond: number;
  reconnectCount: number;
  renderer: string;
  buildStamp: string;
}

const EMPTY: DebugMetrics = {
  fps: 0,
  frameTimeMs: 0,
  mapId: "",
  mapVersion: 0,
  cameraX: 0,
  cameraY: 0,
  zoom: 1,
  visibleTiles: 0,
  visibleBuildings: 0,
  visibleGenoses: 0,
  totalGenoses: 0,
  tick: 0,
  bufferFrames: 0,
  interpolationDelayMs: 0,
  inputAckP50Ms: 0,
  inputAckP95Ms: 0,
  bytesPerSecond: 0,
  reconnectCount: 0,
  renderer: "",
  buildStamp: "dev"
};

/** Developer diagnostics. Never includes tokens or full player records. */
export class DebugOverlay {
  private readonly element: HTMLElement;

  constructor(parent: HTMLElement) {
    this.element = document.createElement("pre");
    this.element.className = "debug-overlay";
    this.element.setAttribute("aria-hidden", "true");
    parent.appendChild(this.element);
    this.update(EMPTY);
  }

  setVisible(visible: boolean): void {
    this.element.hidden = !visible;
  }

  update(metrics: DebugMetrics): void {
    this.element.textContent = [
      `fps ${metrics.fps.toFixed(1)}  frame ${metrics.frameTimeMs.toFixed(2)}ms`,
      `map ${metrics.mapId || "-"} v${metrics.mapVersion}  renderer ${metrics.renderer || "-"}`,
      `camera ${metrics.cameraX.toFixed(1)},${metrics.cameraY.toFixed(1)} zoom ${metrics.zoom.toFixed(2)}`,
      `visible tiles ${metrics.visibleTiles}  buildings ${metrics.visibleBuildings}  genoses ${metrics.visibleGenoses}/${metrics.totalGenoses}`,
      `tick ${metrics.tick}  buffer ${metrics.bufferFrames}  interp ${metrics.interpolationDelayMs}ms`,
      `input ack p50 ${metrics.inputAckP50Ms.toFixed(0)}ms  p95 ${metrics.inputAckP95Ms.toFixed(0)}ms`,
      `rx ${(metrics.bytesPerSecond / 1024).toFixed(1)} KiB/s  reconnects ${metrics.reconnectCount}`,
      `build ${metrics.buildStamp}`
    ].join("\n");
  }

  destroy(): void {
    this.element.remove();
  }
}
