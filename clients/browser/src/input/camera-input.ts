import { GAME_CONSTANTS } from "../config/constants";
import type { Camera } from "../render/camera";
import type { ClientSettings } from "../ui/settings";
import { InputMap, isTypingTarget } from "./input-map";

export interface CameraInputOptions {
  canvas: HTMLCanvasElement;
  camera: Camera;
  getSettings: () => ClientSettings;
  onCancel: () => void;
  onToggleDebug: () => void;
}

/**
 * Keyboard and pointer camera controls. Left click is reserved for targeting;
 * drag pan uses the middle button or left+Alt. Wheel zoom never scrolls the page.
 */
export class CameraInput {
  private readonly map: InputMap;
  private readonly pressed = new Set<string>();
  private drag:
    | { pointerId: number; lastX: number; lastY: number; moved: number }
    | null = null;
  private removeFrame: (() => void) | null = null;
  private disposed = false;

  private readonly onKeyDown = (event: KeyboardEvent): void => {
    if (isTypingTarget(event.target)) return;
    const action = this.map.actionFor(event);
    if (action === null) return;

    if (action === "toggleDebug") {
      event.preventDefault();
      this.options.onToggleDebug();
      return;
    }
    if (action === "cancel") {
      this.options.onCancel();
      return;
    }
    if (action === "zoomIn" || action === "zoomOut") {
      event.preventDefault();
      const factor = action === "zoomIn" ? GAME_CONSTANTS.zoomStep : 1 / GAME_CONSTANTS.zoomStep;
      this.options.camera.setZoom(this.options.camera.zoom * factor);
      return;
    }

    event.preventDefault();
    this.pressed.add(action);
  };

  private readonly onKeyUp = (event: KeyboardEvent): void => {
    const action = this.map.actionFor(event);
    if (action !== null) this.pressed.delete(action);
  };

  private readonly onWheel = (event: WheelEvent): void => {
    if (isTypingTarget(event.target) || isHudTarget(event.target)) return;
    event.preventDefault();
    const canvas = this.options.canvas.getBoundingClientRect();
    const factor =
      event.deltaY < 0 ? GAME_CONSTANTS.zoomStep : 1 / GAME_CONSTANTS.zoomStep;
    this.options.camera.zoomAt(factor, event.clientX - canvas.left, event.clientY - canvas.top);
  };

  private readonly onPointerDown = (event: PointerEvent): void => {
    if (isHudTarget(event.target)) return;
    const panWithAlt = event.button === 0 && event.altKey;
    if (event.button !== 1 && !panWithAlt) return;
    event.preventDefault();
    this.options.canvas.focus();
    this.drag = { pointerId: event.pointerId, lastX: event.clientX, lastY: event.clientY, moved: 0 };
    this.options.canvas.setPointerCapture?.(event.pointerId);
  };

  private readonly onPointerMove = (event: PointerEvent): void => {
    if (this.drag === null || this.drag.pointerId !== event.pointerId) return;
    const dx = event.clientX - this.drag.lastX;
    const dy = event.clientY - this.drag.lastY;
    this.drag.lastX = event.clientX;
    this.drag.lastY = event.clientY;
    this.drag.moved += Math.abs(dx) + Math.abs(dy);
    const camera = this.options.camera;
    const tilesPerPixel = 1 / Math.max(1, camera.zoom);
    camera.panByTiles(-dx * tilesPerPixel, -dy * tilesPerPixel);
  };

  private readonly onPointerUp = (event: PointerEvent): void => {
    if (this.drag === null || this.drag.pointerId !== event.pointerId) return;
    this.options.canvas.releasePointerCapture?.(event.pointerId);
    this.drag = null;
  };

  private readonly onContextMenu = (event: MouseEvent): void => {
    if (isHudTarget(event.target)) return;
    event.preventDefault();
  };

  constructor(
    private readonly options: CameraInputOptions,
    addFrame: (callback: (deltaMs: number) => void) => () => void
  ) {
    this.map = new InputMap(options.getSettings().bindings);
    window.addEventListener("keydown", this.onKeyDown);
    window.addEventListener("keyup", this.onKeyUp);
    options.canvas.addEventListener("wheel", this.onWheel, { passive: false });
    options.canvas.addEventListener("pointerdown", this.onPointerDown);
    window.addEventListener("pointermove", this.onPointerMove);
    window.addEventListener("pointerup", this.onPointerUp);
    options.canvas.addEventListener("contextmenu", this.onContextMenu);
    this.removeFrame = addFrame((deltaMs) => this.update(deltaMs));
  }

  update(deltaMs: number): void {
    if (this.disposed || this.pressed.size === 0) return;
    const settings = this.options.getSettings();
    this.map.update(settings.bindings);

    const seconds = Math.min(0.1, Math.max(0, deltaMs / 1000));
    const camera = this.options.camera;
    const speed = (GAME_CONSTANTS.keyboardPanPixelsPerSecond / Math.max(1, camera.zoom)) * seconds;
    let dx = 0;
    let dy = 0;
    if (this.pressed.has("panLeft")) dx -= speed;
    if (this.pressed.has("panRight")) dx += speed;
    if (this.pressed.has("panUp")) dy -= speed;
    if (this.pressed.has("panDown")) dy += speed;
    if (dx !== 0 || dy !== 0) camera.panByTiles(dx, dy);
  }

  dispose(): void {
    this.disposed = true;
    window.removeEventListener("keydown", this.onKeyDown);
    window.removeEventListener("keyup", this.onKeyUp);
    this.options.canvas.removeEventListener("wheel", this.onWheel);
    this.options.canvas.removeEventListener("pointerdown", this.onPointerDown);
    window.removeEventListener("pointermove", this.onPointerMove);
    window.removeEventListener("pointerup", this.onPointerUp);
    this.options.canvas.removeEventListener("contextmenu", this.onContextMenu);
    this.removeFrame?.();
    this.removeFrame = null;
    this.pressed.clear();
  }
}

function isHudTarget(target: EventTarget | null): boolean {
  return target instanceof Element && target.closest("#hud") !== null;
}
