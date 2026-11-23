export type NotificationKind = "info" | "success" | "warning" | "error";

export interface Notification {
  id: number;
  kind: NotificationKind;
  code: string;
  message: string;
  at: number;
}

export interface NotificationOptions {
  dedupeWindowMs?: number;
  limit?: number;
  now?: () => number;
}

/**
 * Short, typed player messages. Repeated identical messages are coalesced inside
 * a window so a failing connection does not flood the stack.
 */
export class NotificationCenter {
  private nextId = 1;
  private items: Notification[] = [];
  private readonly listeners = new Set<(items: readonly Notification[]) => void>();
  private readonly dedupeWindowMs: number;
  private readonly limit: number;
  private readonly now: () => number;

  constructor(options: NotificationOptions = {}) {
    this.dedupeWindowMs = options.dedupeWindowMs ?? 4_000;
    this.limit = options.limit ?? 5;
    this.now = options.now ?? (() => Date.now());
  }

  get list(): readonly Notification[] {
    return this.items;
  }

  push(kind: NotificationKind, code: string, message: string): Notification | null {
    const now = this.now();
    const duplicate = this.items.find(
      (item) => item.code === code && item.message === message && now - item.at < this.dedupeWindowMs
    );
    if (duplicate !== undefined) return null;

    const notification: Notification = {
      id: this.nextId++,
      kind,
      code,
      message,
      at: now
    };
    this.items = [...this.items, notification].slice(-this.limit);
    this.emit();
    return notification;
  }

  dismiss(id: number): void {
    const next = this.items.filter((item) => item.id !== id);
    if (next.length === this.items.length) return;
    this.items = next;
    this.emit();
  }

  clear(): void {
    if (this.items.length === 0) return;
    this.items = [];
    this.emit();
  }

  subscribe(listener: (items: readonly Notification[]) => void): () => void {
    this.listeners.add(listener);
    listener(this.items);
    return () => this.listeners.delete(listener);
  }

  private emit(): void {
    for (const listener of this.listeners) listener(this.items);
  }
}
