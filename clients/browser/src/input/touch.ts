import type { Camera } from "../render/camera";

export interface TouchPoint {
  id: number;
  x: number;
  y: number;
  startX: number;
  startY: number;
  startedAt: number;
}

export interface TouchGestureCallbacks {
  onPan: (dxPixels: number, dyPixels: number) => void;
  onZoom: (factor: number, x: number, y: number) => void;
  /** A quick, still press. Carries the pointer's client coordinates. */
  onTap?: (x: number, y: number) => void;
  onGestureEnd?: () => void;
}

export interface TouchGestureOptions {
  tapSlopPixels?: number;
  maxTapMs?: number;
  now?: () => number;
}

/**
 * Pure gesture recognizer for one-finger pan, two-finger pinch and tap. A tap
 * is reported through `onTap`; drags and pinches mark the gesture so the adapter
 * can suppress the synthesized click.
 */
export class TouchGestureRecognizer {
  private readonly points = new Map<number, TouchPoint>();
  private readonly tapSlop: number;
  private readonly maxTapMs: number;
  private readonly now: () => number;
  private lastPinchDistance: number | null = null;
  private gesture = false;

  constructor(
    private readonly callbacks: TouchGestureCallbacks,
    options: TouchGestureOptions = {}
  ) {
    this.tapSlop = options.tapSlopPixels ?? 14;
    this.maxTapMs = options.maxTapMs ?? 350;
    this.now = options.now ?? (() => Date.now());
  }

  get active(): number {
    return this.points.size;
  }

  down(id: number, x: number, y: number): void {
    this.points.set(id, {
      id,
      x,
      y,
      startX: x,
      startY: y,
      startedAt: this.now()
    });
    if (this.points.size === 1) {
      this.gesture = false;
      this.lastPinchDistance = null;
    } else {
      this.gesture = true;
      this.lastPinchDistance = this.pinchDistance();
    }
  }

  move(id: number, x: number, y: number): boolean {
    const point = this.points.get(id);
    if (point === undefined) return false;

    const dx = x - point.x;
    const dy = y - point.y;
    point.x = x;
    point.y = y;

    if (this.points.size >= 2) {
      const distance = this.pinchDistance();
      if (this.lastPinchDistance !== null && distance > 0) {
        const center = this.pinchCenter();
        this.callbacks.onZoom(distance / this.lastPinchDistance, center.x, center.y);
      }
      this.lastPinchDistance = distance;
      this.gesture = true;
      return true;
    }

    if (this.movedTooFar(point) || Math.abs(dx) + Math.abs(dy) > 0) {
      this.callbacks.onPan(dx, dy);
      if (this.movedTooFar(point)) this.gesture = true;
      return true;
    }

    return false;
  }

  up(id: number, _time?: number): boolean {
    const point = this.points.get(id);
    if (point === undefined) return false;

    const wasSingle = this.points.size === 1;
    const elapsed = this.now() - point.startedAt;
    const tapped =
      wasSingle &&
      !this.gesture &&
      !this.movedTooFar(point) &&
      elapsed <= this.maxTapMs;

    if (tapped) this.callbacks.onTap?.(point.x, point.y);
    this.points.delete(id);
    if (this.points.size < 2) this.lastPinchDistance = null;
    if (this.points.size === 0) {
      const gesture = this.gesture;
      this.gesture = false;
      if (gesture) this.callbacks.onGestureEnd?.();
      return !tapped && gesture;
    }
    return this.gesture;
  }

  cancelAll(): void {
    this.points.clear();
    this.lastPinchDistance = null;
    this.gesture = false;
  }

  get wasGesture(): boolean {
    return this.gesture;
  }


  private movedTooFar(point: TouchPoint): boolean {
    return (
      Math.abs(point.x - point.startX) > this.tapSlop ||
      Math.abs(point.y - point.startY) > this.tapSlop
    );
  }

  private pinchDistance(): number {
    const [first, second] = [...this.points.values()];
    if (first === undefined || second === undefined) return 0;
    return Math.hypot(second.x - first.x, second.y - first.y);
  }

  private pinchCenter(): { x: number; y: number } {
    const [first, second] = [...this.points.values()];
    if (first === undefined || second === undefined) return { x: 0, y: 0 };
    return { x: (first.x + second.x) / 2, y: (first.y + second.y) / 2 };
  }
}

export interface TouchInputOptions {
  canvas: HTMLCanvasElement;
  camera: Camera;
  getEnabled: () => boolean;
  onGesture?: () => void;
  /** Invoked for a tap; the primary action (select-or-move). */
  onTap?: (x: number, y: number) => void;
}

/**
 * DOM adapter for touch pointers. It never submits intents itself: a tap is
 * reported through `onTap` so the shared pointer path handles selection and
 * movement, and the synthesized click is suppressed to avoid double handling.
 */
export class TouchInput {
  private readonly recognizer: TouchGestureRecognizer;
  private suppressNextClick = false;
  private tapHandled = false;

  private readonly onPointerDown = (event: PointerEvent): void => {
    if (!this.options.getEnabled() || event.pointerType !== "touch") return;
    this.options.canvas.setPointerCapture?.(event.pointerId);
    this.recognizer.down(event.pointerId, event.clientX, event.clientY);
  };

  private readonly onPointerMove = (event: PointerEvent): void => {
    if (event.pointerType !== "touch") return;
    this.recognizer.move(event.pointerId, event.clientX, event.clientY);
  };

  private readonly onPointerUp = (event: PointerEvent): void => {
    if (event.pointerType !== "touch") return;
    const gesture = this.recognizer.up(event.pointerId);
    // A handled tap owns the gesture, so the synthesized click must not run the
    // pointer path again and re-handle the same tile.
    this.suppressNextClick = gesture || this.tapHandled;
    this.tapHandled = false;
    this.options.onGesture?.();
    this.options.canvas.releasePointerCapture?.(event.pointerId);
  };

  private readonly onPointerCancel = (event: PointerEvent): void => {
    if (event.pointerType !== "touch") return;
    this.recognizer.cancelAll();
    this.suppressNextClick = false;
  };

  private readonly onClickCapture = (event: MouseEvent): void => {
    if (!this.suppressNextClick) return;
    this.suppressNextClick = false;
    event.preventDefault();
    event.stopImmediatePropagation();
  };

  constructor(private readonly options: TouchInputOptions) {
    const camera = options.camera;
    this.recognizer = new TouchGestureRecognizer(
      {
        onPan: (dx, dy) => {
          const tilesPerPixel = 1 / Math.max(1, camera.zoom);
          camera.panByTiles(-dx * tilesPerPixel, -dy * tilesPerPixel);
        },
        onZoom: (factor, x, y) => {
          const rect = options.canvas.getBoundingClientRect();
          camera.zoomAt(factor, x - rect.left, y - rect.top);
        },
        onTap: (x, y) => {
          const onTap = options.onTap;
          if (onTap === undefined) return;
          this.tapHandled = true;
          onTap(x, y);
        }
      },
      {}
    );

    options.canvas.addEventListener("pointerdown", this.onPointerDown);
    options.canvas.addEventListener("pointermove", this.onPointerMove);
    options.canvas.addEventListener("pointerup", this.onPointerUp);
    options.canvas.addEventListener("pointercancel", this.onPointerCancel);
    options.canvas.addEventListener("click", this.onClickCapture, true);
  }

  dispose(): void {
    const canvas = this.options.canvas;
    canvas.removeEventListener("pointerdown", this.onPointerDown);
    canvas.removeEventListener("pointermove", this.onPointerMove);
    canvas.removeEventListener("pointerup", this.onPointerUp);
    canvas.removeEventListener("pointercancel", this.onPointerCancel);
    canvas.removeEventListener("click", this.onClickCapture, true);
    this.recognizer.cancelAll();
  }
}
