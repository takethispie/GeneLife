import type { MapDelta, MapSnapshot } from "../../contracts/hub";
import { applyMapDelta, type MapDeltaOutcome } from "./apply-map-delta";
import { decodeMap, type DecodedMap } from "./rle-decoder";

/**
 * Owns the decoded static map. Keyed by `mapId` + `version`; a full snapshot
 * replaces it, a delta advances the version, and a gap invalidates it so
 * dynamic state is not rendered against a stale layout.
 */
export class MapCache {
  private decoded: DecodedMap | null = null;
  private stale = false;

  get current(): DecodedMap | null {
    return this.decoded;
  }

  get isStale(): boolean {
    return this.stale;
  }

  get key(): string | null {
    return this.decoded === null ? null : `${this.decoded.mapId}:${this.decoded.version}`;
  }

  setSnapshot(snapshot: MapSnapshot): DecodedMap {
    this.decoded = decodeMap(snapshot);
    this.stale = false;
    return this.decoded;
  }

  applyDelta(delta: MapDelta): MapDeltaOutcome {
    if (this.decoded === null) {
      return { status: "gap", reason: "no map cached" };
    }

    const outcome = applyMapDelta(this.decoded, delta);
    if (outcome.status === "applied") {
      this.decoded = outcome.map;
      this.stale = false;
    } else {
      this.stale = true;
    }
    return outcome;
  }

  matches(mapId: string, version: number): boolean {
    return this.decoded !== null && this.decoded.mapId === mapId && this.decoded.version === version;
  }

  clear(): void {
    this.decoded = null;
    this.stale = false;
  }
}
