import { AuthError, requestPlayerToken, type FetchLike, type PlayerToken } from "./token-client";

export type SessionState =
  | { status: "anonymous" }
  | { status: "authenticating"; playerId: string }
  | { status: "authenticated"; playerId: string; token: string; expiresAt: number }
  | { status: "expired"; playerId: string }
  | { status: "rejected"; playerId: string; reason: string };

export interface SessionPersistence {
  load(): { playerId: string; token: string; expiresAt: number } | null;
  save(value: { playerId: string; token: string; expiresAt: number }): void;
  clear(): void;
}

const SESSION_KEY = "genelife.session";

/**
 * Persists the token for same-tab reload recovery. This is an explicit XSS
 * tradeoff: never move it to localStorage and never log it.
 */
export class SessionStoragePersistence implements SessionPersistence {
  load(): { playerId: string; token: string; expiresAt: number } | null {
    try {
      const raw = globalThis.sessionStorage?.getItem(SESSION_KEY);
      if (raw === null || raw === undefined) return null;
      const parsed = JSON.parse(raw) as Record<string, unknown>;
      if (
        typeof parsed.playerId !== "string" ||
        typeof parsed.token !== "string" ||
        typeof parsed.expiresAt !== "number"
      ) {
        return null;
      }
      return { playerId: parsed.playerId, token: parsed.token, expiresAt: parsed.expiresAt };
    } catch {
      return null;
    }
  }

  save(value: { playerId: string; token: string; expiresAt: number }): void {
    try {
      globalThis.sessionStorage?.setItem(SESSION_KEY, JSON.stringify(value));
    } catch {
      // Storage can be unavailable (private mode); memory state still works.
    }
  }

  clear(): void {
    try {
      globalThis.sessionStorage?.removeItem(SESSION_KEY);
    } catch {
      // Ignore unavailable storage.
    }
  }
}

export class MemoryPersistence implements SessionPersistence {
  private value: { playerId: string; token: string; expiresAt: number } | null = null;

  load(): { playerId: string; token: string; expiresAt: number } | null {
    return this.value;
  }

  save(value: { playerId: string; token: string; expiresAt: number }): void {
    this.value = value;
  }

  clear(): void {
    this.value = null;
  }
}

export class SessionStore {
  private current: SessionState = { status: "anonymous" };
  private readonly listeners = new Set<(state: SessionState) => void>();

  get state(): SessionState {
    return this.current;
  }

  get token(): string | null {
    return this.current.status === "authenticated" ? this.current.token : null;
  }

  set(state: SessionState): void {
    this.current = state;
    for (const listener of this.listeners) listener(state);
  }

  subscribe(listener: (state: SessionState) => void): () => void {
    this.listeners.add(listener);
    listener(this.current);
    return () => this.listeners.delete(listener);
  }

  isExpiring(now: number, marginMs: number): boolean {
    return this.current.status === "authenticated" && this.current.expiresAt - now < marginMs;
  }
}

export interface SessionControllerOptions {
  apiBaseUrl: string;
  store?: SessionStore;
  persistence?: SessionPersistence;
  fetchImpl?: FetchLike;
  now?: () => number;
  refreshMarginMs?: number;
}

/**
 * Owns the player token lifecycle. The token is kept in memory and mirrored to
 * sessionStorage for reload recovery; it is never written to logs.
 */
export class SessionController {
  readonly store: SessionStore;
  private readonly apiBaseUrl: string;
  private readonly persistence: SessionPersistence;
  private readonly fetchImpl: FetchLike | undefined;
  private readonly now: () => number;
  private readonly refreshMarginMs: number;

  constructor(options: SessionControllerOptions) {
    this.apiBaseUrl = options.apiBaseUrl;
    this.store = options.store ?? new SessionStore();
    this.persistence = options.persistence ?? new SessionStoragePersistence();
    this.fetchImpl = options.fetchImpl;
    this.now = options.now ?? (() => Date.now());
    this.refreshMarginMs = options.refreshMarginMs ?? 5 * 60 * 1000;
  }

  restore(): SessionState {
    const saved = this.persistence.load();
    if (saved === null) {
      this.store.set({ status: "anonymous" });
      return this.store.state;
    }
    if (saved.expiresAt <= this.now()) {
      this.persistence.clear();
      this.store.set({ status: "expired", playerId: saved.playerId });
      return this.store.state;
    }
    this.store.set({
      status: "authenticated",
      playerId: saved.playerId,
      token: saved.token,
      expiresAt: saved.expiresAt
    });
    return this.store.state;
  }

  async signIn(playerId: string): Promise<SessionState> {
    const trimmed = playerId.trim();
    this.store.set({ status: "authenticating", playerId: trimmed });
    try {
      const token = await requestPlayerToken(this.apiBaseUrl, trimmed, this.fetchImpl ?? fetch);
      this.accept(token);
      return this.store.state;
    } catch (error) {
      const reason = error instanceof AuthError ? error.message : "Authentication failed";
      this.persistence.clear();
      this.store.set({ status: "rejected", playerId: trimmed, reason });
      return this.store.state;
    }
  }

  /**
   * Refreshes the token when it is inside the refresh margin. Returns a valid
   * token or null when the session cannot be refreshed.
   */
  async ensureValidToken(): Promise<string | null> {
    const state = this.store.state;
    if (state.status !== "authenticated") return null;

    if (!this.store.isExpiring(this.now(), this.refreshMarginMs)) return state.token;

    try {
      const token = await requestPlayerToken(
        this.apiBaseUrl,
        state.playerId,
        this.fetchImpl ?? fetch
      );
      this.accept(token);
      return token.accessToken;
    } catch {
      this.persistence.clear();
      this.store.set({ status: "expired", playerId: state.playerId });
      return null;
    }
  }

  signOut(): void {
    this.persistence.clear();
    this.store.set({ status: "anonymous" });
  }

  private accept(token: PlayerToken): void {
    this.persistence.save({
      playerId: token.playerId,
      token: token.accessToken,
      expiresAt: token.expiresAt
    });
    this.store.set({
      status: "authenticated",
      playerId: token.playerId,
      token: token.accessToken,
      expiresAt: token.expiresAt
    });
  }
}
