/** Authoritative position for one sim at one server tick. */
import type { AppearanceStatus } from "../contracts/hub";

export interface SimPosition {
  x: number;
  y: number;
  tileX: number;
  tileY: number;
  appearance?: AppearanceStatus | null;
  isPlayerControlled?: boolean;
}

export interface SimFrame {
  tick: number;
  /** Client receive time in milliseconds (`performance.now()` domain). */
  arrivedAt: number;
  positions: Map<number, SimPosition>;
}

export interface RenderSim extends SimPosition {
  networkId: number;
  /** True when the position came from interpolation between two frames. */
  interpolated: boolean;
  /** True when a large error forced a snap to the newer frame. */
  snapped: boolean;
}

export interface InterpolationOptions {
  tickIntervalMs: number;
  interpolationDelayMs: number;
  /** World-space distance above which we snap instead of interpolating. */
  snapDistance: number;
  /** Maximum tick gap we are willing to interpolate across. */
  maxFrameGapTicks: number;
  reducedMotion: boolean;
}

export const DEFAULT_INTERPOLATION_OPTIONS: InterpolationOptions = {
  tickIntervalMs: 40,
  interpolationDelayMs: 120,
  snapDistance: 3,
  maxFrameGapTicks: 3,
  reducedMotion: false
};

export function latestFrame(frames: readonly SimFrame[]): SimFrame | null {
  return frames.length === 0 ? null : (frames[frames.length - 1] ?? null);
}

/**
 * Estimates the server tick to render. The estimate starts from the newest
 * received frame and moves forward with wall time, then rewinds by the
 * interpolation delay. It never renders past the newest frame (no extrapolation).
 */
export function computeRenderTick(
  frames: readonly SimFrame[],
  now: number,
  options: Pick<InterpolationOptions, "tickIntervalMs" | "interpolationDelayMs"> & {
    reducedMotion?: boolean;
  }
): number | null {
  const latest = latestFrame(frames);
  if (latest === null) return null;
  if (options.reducedMotion) return latest.tick;

  const tickMs = Math.max(1, options.tickIntervalMs);
  const elapsedTicks = Math.max(0, now - latest.arrivedAt) / tickMs;
  const delayTicks = Math.max(0, options.interpolationDelayMs) / tickMs;
  const target = latest.tick + elapsedTicks - delayTicks;
  const oldest = frames[0]?.tick ?? latest.tick;
  return Math.min(latest.tick, Math.max(oldest, target));
}

export interface FramePair {
  lower: SimFrame;
  upper: SimFrame;
  /** 0..1 position between the two frames. */
  alpha: number;
}

export function pickFrames(frames: readonly SimFrame[], targetTick: number): FramePair | null {
  if (frames.length === 0) return null;
  if (frames.length === 1) {
    const only = frames[0];
    return only === undefined ? null : { lower: only, upper: only, alpha: 0 };
  }

  let lower = frames[0];
  let upper = frames[frames.length - 1];
  if (lower === undefined || upper === undefined) return null;

  for (let i = 0; i < frames.length - 1; i++) {
    const a = frames[i];
    const b = frames[i + 1];
    if (a === undefined || b === undefined) continue;
    if (targetTick >= a.tick && targetTick <= b.tick) {
      lower = a;
      upper = b;
      break;
    }
    if (targetTick > b.tick) {
      lower = b;
      upper = b;
    }
  }

  const span = upper.tick - lower.tick;
  const alpha = span <= 0 ? 0 : Math.min(1, Math.max(0, (targetTick - lower.tick) / span));
  return { lower, upper, alpha };
}

/**
 * Builds render positions for every sim present in the newer frame. Removed
 * sims disappear immediately because only the newer frame's ids are iterated.
 */
export function interpolateFrames(
  pair: FramePair,
  options: Pick<InterpolationOptions, "snapDistance" | "maxFrameGapTicks">
): Map<number, RenderSim> {
  const result = new Map<number, RenderSim>();
  const { lower, upper, alpha } = pair;

  for (const [networkId, newer] of upper.positions) {
    const older = lower.positions.get(networkId);
    if (older === undefined) {
      result.set(networkId, { ...newer, networkId, interpolated: false, snapped: true });
      continue;
    }

    const tickGap = upper.tick - lower.tick;
    const dx = newer.x - older.x;
    const dy = newer.y - older.y;
    const distance = Math.hypot(dx, dy);
    const mustSnap = tickGap > options.maxFrameGapTicks || distance > options.snapDistance;

    if (mustSnap || alpha <= 0 || alpha >= 1) {
      result.set(networkId, {
        ...newer,
        networkId,
        interpolated: false,
        snapped: mustSnap && distance > 0
      });
      continue;
    }

    result.set(networkId, {
      networkId,
      x: older.x + dx * alpha,
      y: older.y + dy * alpha,
      tileX: newer.tileX,
      tileY: newer.tileY,
      appearance: newer.appearance,
      isPlayerControlled: newer.isPlayerControlled,
      interpolated: true,
      snapped: false
    });
  }

  return result;
}
