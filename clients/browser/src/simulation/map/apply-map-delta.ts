import type { MapDelta } from "../../contracts/hub";
import type { DecodedMap } from "./rle-decoder";

export type MapDeltaOutcome =
  | { status: "applied"; map: DecodedMap }
  | { status: "gap"; reason: string }
  | { status: "mismatch"; reason: string };

/**
 * Applies a building-only map delta to a decoded map. A version gap or map id
 * mismatch must force a full map refresh; dynamic state is not trusted until
 * the map is replaced.
 */
export function applyMapDelta(current: DecodedMap, delta: MapDelta): MapDeltaOutcome {
  if (delta.mapId !== current.mapId) {
    return {
      status: "mismatch",
      reason: `map id changed from ${current.mapId} to ${delta.mapId}`
    };
  }
  if (delta.fromVersion !== current.version) {
    return {
      status: "gap",
      reason: `map version gap: at ${current.version}, delta from ${delta.fromVersion}`
    };
  }
  if (delta.toVersion < delta.fromVersion) {
    return {
      status: "gap",
      reason: `map version moved backwards: ${delta.fromVersion} -> ${delta.toVersion}`
    };
  }

  const buildings = new Map(current.buildings);
  for (const building of delta.added) buildings.set(building.networkId, building);
  for (const building of delta.updated) buildings.set(building.networkId, building);
  for (const id of delta.removed) buildings.delete(id);

  return {
    status: "applied",
    map: {
      ...current,
      version: delta.toVersion,
      buildings
    }
  };
}
