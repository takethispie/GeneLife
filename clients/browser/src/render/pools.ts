/**
 * Minimal display-object pool. Objects are hidden rather than destroyed so
 * panning does not allocate a new sprite per tile or sim.
 */
export class DisplayPool<T extends { visible: boolean; destroy(options?: unknown): void }> {
  private readonly items: T[] = [];
  private used = 0;

  constructor(private readonly create: () => T) {}

  get size(): number {
    return this.items.length;
  }

  get activeCount(): number {
    return this.used;
  }

  begin(): void {
    this.used = 0;
  }

  acquire(): T {
    let item = this.items[this.used];
    if (item === undefined) {
      item = this.create();
      this.items.push(item);
    }
    this.used++;
    item.visible = true;
    return item;
  }

  end(): void {
    for (let i = this.used; i < this.items.length; i++) {
      const item = this.items[i];
      if (item !== undefined) item.visible = false;
    }
  }

  destroy(): void {
    for (const item of this.items) item.destroy();
    this.items.length = 0;
    this.used = 0;
  }
}
