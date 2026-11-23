/** Measures approximate received throughput from payload sizes. */
export class TrafficMeter {
  private samples: { at: number; bytes: number }[] = [];

  constructor(private readonly windowMs = 5_000) {}

  add(bytes: number, now = Date.now()): void {
    if (!Number.isFinite(bytes) || bytes <= 0) return;
    this.samples.push({ at: now, bytes });
    this.prune(now);
  }

  addPayload(payload: unknown, now = Date.now()): void {
    try {
      this.add(JSON.stringify(payload).length, now);
    } catch {
      // Non-serializable payloads do not contribute to the estimate.
    }
  }

  bytesPerSecond(now = Date.now()): number {
    this.prune(now);
    const total = this.samples.reduce((sum, sample) => sum + sample.bytes, 0);
    return (total * 1000) / this.windowMs;
  }

  private prune(now: number): void {
    const cutoff = now - this.windowMs;
    while (this.samples.length > 0 && (this.samples[0]?.at ?? now) < cutoff) this.samples.shift();
  }
}
