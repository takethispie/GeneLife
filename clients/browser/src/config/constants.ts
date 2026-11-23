export const GAME_CONSTANTS = {
  /** Pixels per world unit at zoom 1. */
  pixelsPerTile: 32,
  minZoom: 12,
  maxZoom: 128,
  zoomStep: 1.15,
  keyboardPanPixelsPerSecond: 520,
  viewportDebounceMs: 200,
  viewportChangeThreshold: 1,
  localIntentIntervalMs: 90,
  intentTimeoutMs: 4000,
  hudUpdateHz: 10,
  notificationDedupeMs: 4000,
  defaultInterpolationDelayMs: 120,
  snapDistanceTiles: 3,
  maxFrameGapTicks: 3,
  /** Interaction and sign-in hint only; the server owns the real range check. */
  interactionHintRadius: 2
} as const;
