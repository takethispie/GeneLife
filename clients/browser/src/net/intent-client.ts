import type { Intent, IntentResult, MoveIntent, UseBuildingIntent } from "../contracts/hub";

export interface SequenceStore {
  load(): number;
  save(value: number): void;
}

export class SessionSequenceStore implements SequenceStore {
  constructor(private readonly key = "genelife.intentSequence") {}

  load(): number {
    try {
      const raw = globalThis.sessionStorage?.getItem(this.key);
      const value = raw === null || raw === undefined ? 0 : Number(raw);
      return Number.isFinite(value) && value >= 0 ? Math.floor(value) : 0;
    } catch {
      return 0;
    }
  }

  save(value: number): void {
    try {
      globalThis.sessionStorage?.setItem(this.key, String(value));
    } catch {
      // Storage unavailable; the in-memory counter still stays monotonic.
    }
  }
}

export class MemorySequenceStore implements SequenceStore {
  private value = 0;

  load(): number {
    return this.value;
  }

  save(value: number): void {
    this.value = value;
  }
}

export type SubmitOutcome =
  | { status: "sent"; result: IntentResult }
  | { status: "rate-limited"; message: string }
  | { status: "not-ready"; message: string }
  | { status: "failed"; message: string };

export interface IntentClientOptions {
  invoke: (intent: Intent) => Promise<IntentResult>;
  playerId: () => string | null;
  ownSimNetworkId: () => number;
  sequenceStore?: SequenceStore;
  onResult?: (intent: Intent, result: IntentResult) => void;
  onRejected?: (code: string, message: string, intent: Intent | undefined) => void;
  minIntervalMs?: number;
  now?: () => number;
}

interface PendingIntent {
  intent: Intent;
  sentAt: number;
}

/**
 * Builds tile-based intents with a monotonic sequence, applies a local UX rate
 * limit, and reports admission asynchronously. It never mutates local position.
 */
export class IntentClient {
  private readonly invoke: (intent: Intent) => Promise<IntentResult>;
  private readonly playerId: () => string | null;
  private readonly ownSimNetworkId: () => number;
  private readonly sequenceStore: SequenceStore;
  private readonly onResult: ((intent: Intent, result: IntentResult) => void) | undefined;
  private readonly onRejected:
    | ((code: string, message: string, intent: Intent | undefined) => void)
    | undefined;
  private readonly minIntervalMs: number;
  private readonly now: () => number;
  private readonly pending = new Map<number, PendingIntent>();
  private sequence: number;
  private lastSentAt = 0;

  constructor(options: IntentClientOptions) {
    this.invoke = options.invoke;
    this.playerId = options.playerId;
    this.ownSimNetworkId = options.ownSimNetworkId;
    this.sequenceStore = options.sequenceStore ?? new SessionSequenceStore();
    this.onResult = options.onResult;
    this.onRejected = options.onRejected;
    this.minIntervalMs = options.minIntervalMs ?? 90;
    this.now = options.now ?? (() => Date.now());
    this.sequence = Math.max(0, this.sequenceStore.load());
  }

  get lastSequence(): number {
    return this.sequence;
  }

  async moveTo(targetTileX: number, targetTileY: number): Promise<SubmitOutcome> {
    const context = this.context();
    if (context === null) return contextError();

    const intent: MoveIntent = {
      type: "move",
      playerId: context.playerId,
      sequence: this.nextSequence(),
      simNetworkId: context.simNetworkId,
      targetTileX: Math.trunc(targetTileX),
      targetTileY: Math.trunc(targetTileY)
    };

    return this.submit(intent);
  }

  async useBuilding(buildingNetworkId: number): Promise<SubmitOutcome> {
    const context = this.context();
    if (context === null) return contextError();

    const intent: UseBuildingIntent = {
      type: "useBuilding",
      playerId: context.playerId,
      sequence: this.nextSequence(),
      simNetworkId: context.simNetworkId,
      buildingNetworkId: buildingNetworkId
    };

    return this.submit(intent);
  }

  /** Called by the network layer when the hub reports an asynchronous rejection. */
  handleRejected(sequence: number, code: string, message: string): void {
    const pending = this.pending.get(sequence);
    this.pending.delete(sequence);
    this.onRejected?.(code, message, pending?.intent);
  }

  private async submit(intent: Intent): Promise<SubmitOutcome> {
    const now = this.now();
    if (now - this.lastSentAt < this.minIntervalMs) {
      return { status: "rate-limited", message: "Slow down a moment" };
    }
    this.lastSentAt = now;
    this.pending.set(intent.sequence, { intent, sentAt: now });
    this.prunePending(now);

    try {
      const result = await this.invoke(intent);
      this.onResult?.(intent, result);
      if (result.success) return { status: "sent", result };
      this.pending.delete(intent.sequence);
      return { status: "failed", message: result.error ?? "Action not accepted" };
    } catch (error) {
      this.pending.delete(intent.sequence);
      return {
        status: "failed",
        message: error instanceof Error ? error.message : "Action not accepted"
      };
    }
  }

  private context(): { playerId: string; simNetworkId: number } | null {
    const playerId = this.playerId();
    const simNetworkId = this.ownSimNetworkId();
    if (playerId === null || playerId.trim() === "" || simNetworkId <= 0) return null;
    return { playerId, simNetworkId };
  }

  private nextSequence(): number {
    this.sequence = Math.max(this.sequence, this.sequenceStore.load()) + 1;
    this.sequenceStore.save(this.sequence);
    return this.sequence;
  }

  private prunePending(now: number): void {
    for (const [sequence, pending] of this.pending) {
      if (now - pending.sentAt > 60_000) this.pending.delete(sequence);
    }
  }
}

function contextError(): SubmitOutcome {
  return {
    status: "not-ready",
    message: "Your sim is not ready yet"
  };
}
