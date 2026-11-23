import type { SetViewportRequest, ViewportResult } from "../contracts/hub";

export interface TileRect {
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
}

/**
 * Converts a visible tile rectangle into the server's center/radius contract.
 * The server clamps both values; the client treats the returned result as
 * authoritative.
 */
export function visibleRectToViewport(rect: TileRect): SetViewportRequest {
  const centerX = Math.round((rect.minX + rect.maxX) / 2);
  const centerY = Math.round((rect.minY + rect.maxY) / 2);
  const width = Math.max(1, rect.maxX - rect.minX + 1);
  const height = Math.max(1, rect.maxY - rect.minY + 1);
  return { centerX, centerY, radius: Math.ceil(Math.max(width, height) / 2) };
}

export interface ViewportSynchronizerOptions {
  setViewport: (request: SetViewportRequest) => Promise<ViewportResult>;
  onResult?: (result: ViewportResult) => void;
  onError?: (error: string) => void;
  debounceMs?: number;
  changeThreshold?: number;
}

export interface EffectiveViewport {
  centerX: number;
  centerY: number;
  radius: number;
}

/**
 * Debounces camera-derived viewport updates, skips sub-threshold changes, and
 * stores the server's clamped result rather than the raw request.
 */
export class ViewportSynchronizer {
  private readonly setViewport: (request: SetViewportRequest) => Promise<ViewportResult>;
  private readonly onResult: ((result: ViewportResult) => void) | undefined;
  private readonly onError: ((error: string) => void) | undefined;
  private readonly debounceMs: number;
  private readonly changeThreshold: number;
  private timer: ReturnType<typeof setTimeout> | null = null;
  private pending: SetViewportRequest | null = null;
  private effective: EffectiveViewport | null = null;
  private inFlight = false;
  private requests = 0;
  private errors = 0;
  private clamped = 0;

  constructor(options: ViewportSynchronizerOptions) {
    this.setViewport = options.setViewport;
    this.onResult = options.onResult;
    this.onError = options.onError;
    this.debounceMs = options.debounceMs ?? 200;
    this.changeThreshold = options.changeThreshold ?? 1;
  }

  get effectiveViewport(): EffectiveViewport | null {
    return this.effective;
  }

  get stats(): { requests: number; errors: number; clamped: number } {
    return { requests: this.requests, errors: this.errors, clamped: this.clamped };
  }

  /** Schedules a debounced viewport update. */
  update(request: SetViewportRequest): void {
    if (this.isSubThreshold(request)) return;
    if (this.pending !== null && sameViewport(this.pending, request)) return;
    this.pending = request;
    if (this.timer !== null) clearTimeout(this.timer);
    this.timer = setTimeout(() => {
      this.timer = null;
      void this.flush();
    }, this.debounceMs);
  }

  /** Sends immediately, for reconnect or map replacement resyncs. */
  async syncNow(request: SetViewportRequest): Promise<void> {
    if (this.timer !== null) {
      clearTimeout(this.timer);
      this.timer = null;
    }
    this.pending = request;
    await this.flush();
  }

  /** Resends the last effective viewport, for reconnect recovery. */
  async resync(): Promise<void> {
    if (this.effective === null) return;
    await this.syncNow(this.effective);
  }

  async flush(): Promise<void> {
    if (this.inFlight || this.pending === null) return;
    const request = this.pending;
    this.pending = null;
    this.inFlight = true;
    this.requests++;

    try {
      const result = await this.setViewport(request);
      if (!result.success) {
        this.errors++;
        this.onError?.(result.error ?? "Viewport update was rejected");
        return;
      }
      if (
        result.centerX !== request.centerX ||
        result.centerY !== request.centerY ||
        result.radius !== request.radius
      ) {
        this.clamped++;
      }
      this.effective = {
        centerX: result.centerX,
        centerY: result.centerY,
        radius: result.radius
      };
      this.onResult?.(result);
    } catch (error) {
      this.errors++;
      this.onError?.(error instanceof Error ? error.message : "Viewport update failed");
    } finally {
      this.inFlight = false;
      if (this.pending !== null && this.timer === null) {
        this.timer = setTimeout(() => {
          this.timer = null;
          void this.flush();
        }, this.debounceMs);
      }
    }
  }

  reset(): void {
    if (this.timer !== null) clearTimeout(this.timer);
    this.timer = null;
    this.pending = null;
    this.effective = null;
  }

  private isSubThreshold(request: SetViewportRequest): boolean {
    if (this.effective === null) return false;
    return (
      Math.abs(request.centerX - this.effective.centerX) <= this.changeThreshold &&
      Math.abs(request.centerY - this.effective.centerY) <= this.changeThreshold &&
      Math.abs(request.radius - this.effective.radius) <= this.changeThreshold
    );
  }
}

export function sameViewport(a: SetViewportRequest, b: SetViewportRequest): boolean {
  return a.centerX === b.centerX && a.centerY === b.centerY && a.radius === b.radius;
}
