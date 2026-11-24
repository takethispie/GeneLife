export interface LatencySample {
  inputAt: number;
  ackAt: number;
  kind: string;
}

export interface LatencyStats {
  count: number;
  p50Ms: number;
  p95Ms: number;
  maxMs: number;
}

/**
 * Own-player input-to-ack instrumentation. It records admission round trips only;
 * it never predicts or mutates simulation state.
 */
export class LatencyRecorder {
  private readonly samples: LatencySample[] = [];

  constructor(private readonly capacity = 200) {}

  get size(): number {
    return this.samples.length;
  }

  record(inputAt: number, ackAt: number, kind = "intent"): LatencySample | null {
    if (!Number.isFinite(inputAt) || !Number.isFinite(ackAt) || ackAt < inputAt) return null;
    const sample = { inputAt, ackAt, kind };
    this.samples.push(sample);
    if (this.samples.length > this.capacity) this.samples.shift();
    return sample;
  }

  stats(): LatencyStats {
    if (this.samples.length === 0) {
      return { count: 0, p50Ms: 0, p95Ms: 0, maxMs: 0 };
    }
    const values = this.samples.map((sample) => sample.ackAt - sample.inputAt).sort((a, b) => a - b);
    return {
      count: values.length,
      p50Ms: percentile(values, 0.5),
      p95Ms: percentile(values, 0.95),
      maxMs: values[values.length - 1] ?? 0
    };
  }

  reset(): void {
    this.samples.length = 0;
  }
}

export function percentile(sortedValues: readonly number[], fraction: number): number {
  if (sortedValues.length === 0) return 0;
  const index = Math.min(
    sortedValues.length - 1,
    Math.max(0, Math.ceil(fraction * sortedValues.length) - 1)
  );
  return sortedValues[index] ?? 0;
}
