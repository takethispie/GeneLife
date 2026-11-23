export interface RecoveryActions {
  /** Refresh auth if needed; false means recovery cannot continue. */
  ensureAuth: () => Promise<boolean>;
  /** True when the cached map matches the server's currently advertised version. */
  hasFreshMap: () => boolean;
  lastAppliedTick: () => number;
  /** Age of the last applied tick in milliseconds, or Infinity when unknown. */
  lastTickAgeMs: () => number;
  requestMapSnapshot: () => Promise<void>;
  requestSnapshot: (sinceTick: number | null) => Promise<void>;
}

export type RecoveryOutcome = "recovered" | "auth-failed" | "failed";

export interface RecoveryOptions {
  /** Resume from a tick only when it is this recent; otherwise request a full snapshot. */
  tickRetentionMs?: number;
  onError?: (message: string) => void;
}

/**
 * Recovers a session in a fixed order: auth, map freshness, then world state.
 * A stale tick is never resumed; a full snapshot is safer than guessing.
 */
export async function recoverSession(
  actions: RecoveryActions,
  options: RecoveryOptions = {}
): Promise<RecoveryOutcome> {
  const retention = options.tickRetentionMs ?? 5_000;

  try {
    if (!(await actions.ensureAuth())) return "auth-failed";

    if (!actions.hasFreshMap()) {
      await actions.requestMapSnapshot();
    }

    const tick = actions.lastAppliedTick();
    const age = actions.lastTickAgeMs();
    const since = tick > 0 && age <= retention ? tick : null;
    await actions.requestSnapshot(since);
    return "recovered";
  } catch (error) {
    const message = error instanceof Error ? error.message : "Recovery failed";
    options.onError?.(message);
    return "failed";
  }
}

/**
 * Bounded exponential backoff with a cap. Used for reconnect/manual retry and to
 * avoid tight loops after `SnapshotRejected`.
 */
export class BoundedBackoff {
  private index = 0;

  constructor(
    private readonly delaysMs: readonly number[] = [250, 1_000, 2_000, 5_000, 10_000],
    private readonly jitter: () => number = Math.random
  ) {
    if (delaysMs.length === 0) throw new Error("Backoff needs at least one delay");
  }

  get attempts(): number {
    return this.index;
  }

  next(): number {
    const base = this.delaysMs[Math.min(this.index, this.delaysMs.length - 1)] ?? 1_000;
    this.index++;
    const jitterRatio = 0.2;
    const factor = 1 - jitterRatio + this.jitter() * jitterRatio * 2;
    return Math.max(0, Math.round(base * factor));
  }

  reset(): void {
    this.index = 0;
  }
}
