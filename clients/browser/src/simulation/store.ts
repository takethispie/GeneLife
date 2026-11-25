import type {
  CompanyStatus,
  MapDelta,
  MapSnapshot,
  GenosStatus,
  WorldSnapshot,
  WorldSnapshotDelta
} from "../contracts/hub";
import { MapCache } from "./map/map-cache";
import type { MapDeltaOutcome } from "./map/apply-map-delta";
import type { DecodedMap } from "./map/rle-decoder";
import {
  computeRenderTick,
  DEFAULT_INTERPOLATION_OPTIONS,
  interpolateFrames,
  latestFrame,
  pickFrames,
  type InterpolationOptions,
  type RenderGenos,
  type GenosFrame,
  type GenosPosition
} from "./interpolation";

export type ApplyStatus = "applied" | "stale" | "gap" | "map-stale";

export interface ApplyResult {
  status: ApplyStatus;
  tick: number;
  requestFullSnapshot?: boolean;
  requestMapSnapshot?: boolean;
  reason?: string;
}

export interface SimulationMetrics {
  appliedTicks: number;
  fullSnapshots: number;
  appliedDeltas: number;
  staleDeltas: number;
  gaps: number;
  mapDeltas: number;
  mapGaps: number;
  lastTick: number;
  lastMapVersion: number;
  frames: number;
  genoses: number;
  companies: number;
}

export interface RenderState {
  tick: number;
  targetTick: number;
  genoses: Map<number, RenderGenos>;
  frameCount: number;
  interpolationDelayMs: number;
}

const FRAME_RETENTION_MS = 1000;
const FRAME_LIMIT = 64;

/**
 * Mutable, frame-oriented cache of server world state. It is deliberately not a
 * UI store: Pixi reads it on the render tick, the HUD reads coalesced values,
 * and network callbacks only mutate it and mark work dirty.
 */
export class SimulationStore {
  readonly mapCache = new MapCache();

  private genosesById = new Map<number, GenosStatus>();
  private companiesById = new Map<number, CompanyStatus>();
  private frames: GenosFrame[] = [];
  private lastAppliedTick = 0;
  private ownGenosNetworkId = 0;
  private mapGap = false;
  private metrics: SimulationMetrics = {
    appliedTicks: 0,
    fullSnapshots: 0,
    appliedDeltas: 0,
    staleDeltas: 0,
    gaps: 0,
    mapDeltas: 0,
    mapGaps: 0,
    lastTick: 0,
    lastMapVersion: 0,
    frames: 0,
    genoses: 0,
    companies: 0
  };

  get map(): DecodedMap | null {
    return this.mapCache.current;
  }

  get genoses(): ReadonlyMap<number, GenosStatus> {
    return this.genosesById;
  }

  get companies(): ReadonlyMap<number, CompanyStatus> {
    return this.companiesById;
  }

  get tick(): number {
    return this.lastAppliedTick;
  }

  get ownGenosId(): number {
    return this.ownGenosNetworkId;
  }

  get isMapStale(): boolean {
    return this.mapGap;
  }

  setOwnGenos(networkId: number): void {
    this.ownGenosNetworkId = networkId;
  }

  getStats(): SimulationMetrics {
    return {
      ...this.metrics,
      lastTick: this.lastAppliedTick,
      lastMapVersion: this.map?.version ?? 0,
      frames: this.frames.length,
      genoses: this.genosesById.size,
      companies: this.companiesById.size
    };
  }

  applyMapSnapshot(snapshot: MapSnapshot): DecodedMap {
    const map = this.mapCache.setSnapshot(snapshot);
    this.mapGap = false;
    this.metrics.lastMapVersion = map.version;
    return map;
  }

  applyMapDelta(delta: MapDelta): MapDeltaOutcome {
    const outcome = this.mapCache.applyDelta(delta);
    if (outcome.status === "applied") {
      this.metrics.mapDeltas++;
      this.metrics.lastMapVersion = outcome.map.version;
      this.mapGap = false;
    } else {
      this.mapGap = true;
      this.metrics.mapGaps++;
    }
    return outcome;
  }

  /**
   * Replaces world state atomically. Returns `stale` for an older full snapshot
   * so a delayed reconnect payload cannot rewind the view.
   */
  applySnapshot(snapshot: WorldSnapshot, now: number): ApplyResult {
    if (this.mapGap) {
      return {
        status: "map-stale",
        tick: snapshot.tick,
        requestMapSnapshot: true,
        reason: "map refresh pending"
      };
    }
    if (snapshot.tick < this.lastAppliedTick) {
      this.metrics.staleDeltas++;
      return { status: "stale", tick: snapshot.tick, reason: "snapshot older than applied tick" };
    }

    const nextGenoses = new Map<number, GenosStatus>();
    for (const genos of snapshot.genoses) nextGenoses.set(genos.networkId, genos);
    const nextCompanies = new Map<number, CompanyStatus>();
    for (const company of snapshot.companies) nextCompanies.set(company.networkId, company);

    this.genosesById = nextGenoses;
    this.companiesById = nextCompanies;
    this.setAppliedTick(snapshot.tick, now);
    this.metrics.fullSnapshots++;
    return { status: "applied", tick: snapshot.tick };
  }

  applyDelta(delta: WorldSnapshotDelta, now: number): ApplyResult {
    if (this.mapGap) {
      return {
        status: "map-stale",
        tick: delta.tick,
        requestMapSnapshot: true,
        reason: "map refresh pending"
      };
    }

    if (delta.full) {
      return this.applySnapshot(
        {
          tick: delta.tick,
          version: delta.version,
          genoses: delta.genoses,
          companies: delta.companies
        },
        now
      );
    }

    if (delta.tick <= this.lastAppliedTick) {
      this.metrics.staleDeltas++;
      return { status: "stale", tick: delta.tick, reason: "delta tick already applied" };
    }

    if (delta.sinceTick !== this.lastAppliedTick) {
      this.metrics.gaps++;
      return {
        status: "gap",
        tick: delta.tick,
        requestFullSnapshot: true,
        reason: `delta since ${delta.sinceTick}, applied ${this.lastAppliedTick}`
      };
    }

    for (const genos of delta.genoses) this.genosesById.set(genos.networkId, genos);
    for (const company of delta.companies) this.companiesById.set(company.networkId, company);
    for (const id of delta.removedGenoses) this.genosesById.delete(id);
    for (const id of delta.removedCompanies) this.companiesById.delete(id);

    this.setAppliedTick(delta.tick, now);
    this.metrics.appliedDeltas++;
    return { status: "applied", tick: delta.tick };
  }

  /**
   * Computes render positions for the current wall-clock time. Never extrapolates
   * past the newest frame; sparse gaps and large corrections snap.
   */
  getRenderState(now: number, options: Partial<InterpolationOptions> = {}): RenderState | null {
    const opts: InterpolationOptions = { ...DEFAULT_INTERPOLATION_OPTIONS, ...options };
    if (opts.reducedMotion) opts.interpolationDelayMs = 0;

    const targetTick = computeRenderTick(this.frames, now, opts);
    const latest = latestFrame(this.frames);
    if (targetTick === null || latest === null) return null;

    const pair = pickFrames(this.frames, targetTick);
    if (pair === null) return null;

    return {
      tick: latest.tick,
      targetTick,
      genoses: interpolateFrames(pair, opts),
      frameCount: this.frames.length,
      interpolationDelayMs: opts.interpolationDelayMs
    };
  }

  positionOf(networkId: number): GenosPosition | null {
    const genos = this.genosesById.get(networkId);
    return genos === undefined
      ? null
      : { x: genos.x, y: genos.y, tileX: genos.tileX, tileY: genos.tileY };
  }

  reset(): void {
    this.mapCache.clear();
    this.genosesById = new Map();
    this.companiesById = new Map();
    this.frames = [];
    this.lastAppliedTick = 0;
    this.ownGenosNetworkId = 0;
    this.mapGap = false;
    this.metrics = {
      appliedTicks: 0,
      fullSnapshots: 0,
      appliedDeltas: 0,
      staleDeltas: 0,
      gaps: 0,
      mapDeltas: 0,
      mapGaps: 0,
      lastTick: 0,
      lastMapVersion: 0,
      frames: 0,
      genoses: 0,
      companies: 0
    };
  }

  private setAppliedTick(tick: number, now: number): void {
    if (tick > this.lastAppliedTick) {
      this.lastAppliedTick = tick;
      this.metrics.appliedTicks++;
    } else if (this.metrics.appliedTicks === 0) {
      this.metrics.appliedTicks = 1;
    }

    if (!this.frames.some((frame) => frame.tick === tick)) {
      this.frames.push({ tick, arrivedAt: now, positions: this.copyPositions() });
      this.frames.sort((a, b) => a.tick - b.tick);
    }

    const cutoff = now - FRAME_RETENTION_MS;
    while (this.frames.length > FRAME_LIMIT) this.frames.shift();
    while (this.frames.length > 1 && (this.frames[0]?.arrivedAt ?? now) < cutoff) {
      this.frames.shift();
    }
  }

  private copyPositions(): Map<number, GenosPosition> {
    const positions = new Map<number, GenosPosition>();
    for (const genos of this.genosesById.values()) {
      positions.set(genos.networkId, {
        x: genos.x,
        y: genos.y,
        tileX: genos.tileX,
        tileY: genos.tileY,
        appearance: genos.appearance,
        isPlayerControlled: genos.isPlayerControlled
      });
    }
    return positions;
  }
}
