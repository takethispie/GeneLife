import type { MapBuilding } from "../contracts/hub";

export interface BuildingDetails {
  name: string;
  meta: string;
  owner: string;
  hint: string;
}

/**
 * Presentation text for the selection panel. Building names and districts are
 * server-provided and are always rendered with `textContent`.
 */
export function describeBuilding(building: MapBuilding): BuildingDetails {
  return {
    name: building.name || "Building",
    meta:
      `${building.kind || "building"} · ${building.district || "no district"} · ` +
      `${building.width}×${building.height} @ (${building.x}, ${building.y}) · ` +
      `entry (${building.entryX}, ${building.entryY})`,
    owner:
      building.ownerNetworkId === undefined || building.ownerNetworkId === null
        ? "No owner"
        : `Owner #${building.ownerNetworkId}`,
    hint: "Interact walks your genos to the entrance when out of range. The server still checks range and reports a refusal."
  };
}
