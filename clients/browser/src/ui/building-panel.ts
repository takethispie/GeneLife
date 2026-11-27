import type { CompanyStatus, GenosStatus, MapBuilding } from "../contracts/hub";

/**
 * The PO's settled cap (2026-11-27): at most this many genoses are listed in
 * one occupant list, followed by a `+N more` line. Do not change it without a
 * new PO decision.
 */
export const OCCUPANT_LIST_LIMIT = 5;

/** The world data the panel needs to resolve owners and occupants. */
export interface WorldPopulation {
  genoses: Iterable<GenosStatus>;
  companies: Iterable<CompanyStatus>;
}

/** A building class that houses genoses (residents). */
const RESIDENTIAL_KINDS = new Set(["residential", "house", "housing", "home", "apartment"]);

/** A building class a company occupies (workers). */
const COMPANY_KINDS = new Set(["office", "store", "shop", "company"]);

export interface OccupantRow {
  networkId: number;
  /** Server-provided genos name, rendered with `textContent`. */
  name: string;
  /** A worker's role (`jobPosition`) or current action; empty for residents. */
  detail: string;
  /** True when the genos' position lies inside the building footprint. */
  present: boolean;
}

export interface OccupantGroup {
  /** Housing unit id, or null when the server did not send one. */
  unitNetworkId: number | null;
  /** Unit heading (`housingUnitNumber`, else `Unit #<id>`); empty for workers. */
  label: string;
  rows: OccupantRow[];
  /** Genoses in this group beyond `OCCUPANT_LIST_LIMIT`; zero when not truncated. */
  hiddenCount: number;
}

export interface BuildingOccupants {
  kind: "residents" | "workers";
  title: string;
  groups: OccupantGroup[];
  /** The explicit "Nobody lives/works here" line, or null when someone is listed. */
  empty: string | null;
}

export interface BuildingDetails {
  name: string;
  meta: string;
  owner: string;
  hint: string;
  /**
   * Residents for housing, workers for company buildings, and null for classes
   * that have no occupants by design (schools, civic lots, unknown kinds) so
   * no empty heading is rendered for them.
   */
  occupants: BuildingOccupants | null;
}

const NO_POPULATION: WorldPopulation = { genoses: [], companies: [] };

/**
 * Presentation text for the selection panel. Building names and districts are
 * server-provided and are always rendered with `textContent`.
 */
export function describeBuilding(
  building: MapBuilding,
  population: WorldPopulation = NO_POPULATION
): BuildingDetails {
  // `population` may be built from one-shot iterators (a Map's `values()`), so
  // materialize it once: the owner lookup runs before the occupancy scan and
  // must not consume the genos stream.
  const resolved: WorldPopulation = {
    genoses: [...population.genoses],
    companies: [...population.companies]
  };
  return {
    name: building.name || "Building",
    meta:
      `${building.kind || "building"} · ${building.district || "no district"} · ` +
      `${building.width}×${building.height} @ (${building.x}, ${building.y}) · ` +
      `entry (${building.entryX}, ${building.entryY})`,
    owner: describeOwner(building, resolved),
    hint: "Interact walks your genos to the entrance when out of range. The server still checks range and reports a refusal.",
    occupants: describeOccupants(building, resolved)
  };
}

/**
 * Resolves `ownerNetworkId` against the client's snapshot: genoses first, then
 * companies (both use the one network-id registry). An id that resolves to
 * neither keeps a neutral, non-invented label. Never throws.
 */
export function describeOwner(building: MapBuilding, population: WorldPopulation): string {
  const id = building.ownerNetworkId;
  if (id === undefined || id === null) return "No owner";
  for (const genos of population.genoses) {
    if (genos.networkId === id) return genos.name || `Genos #${id}`;
  }
  for (const company of population.companies) {
    if (company.networkId === id) return company.name || `Company #${id}`;
  }
  return `Owner #${id} (unknown)`;
}

/**
 * Half-open footprint test: the footprint covers `x .. x + width - 1`. A genos
 * standing exactly on the far edge (`x + width`) is outside, so a genos that
 * straddles the boundary cannot flicker between "present" and "away" between
 * frames.
 */
export function isInsideFootprint(building: MapBuilding, genos: GenosStatus): boolean {
  return (
    genos.x >= building.x &&
    genos.x < building.x + building.width &&
    genos.y >= building.y &&
    genos.y < building.y + building.height
  );
}

function normalizeKind(kind: string): string {
  return kind.trim().toLowerCase();
}

function describeOccupants(
  building: MapBuilding,
  population: WorldPopulation
): BuildingOccupants | null {
  const kind = normalizeKind(building.kind);

  if (RESIDENTIAL_KINDS.has(kind)) {
    const residents = [...population.genoses].filter(
      (genos) =>
        !genos.isDead &&
        genos.housingBuildingNetworkId !== undefined &&
        genos.housingBuildingNetworkId !== null &&
        genos.housingBuildingNetworkId === building.networkId
    );
    return {
      kind: "residents",
      title: "Residents",
      groups: groupResidents(building, residents),
      empty: residents.length === 0 ? "Nobody lives here" : null
    };
  }

  if (COMPANY_KINDS.has(kind)) {
    const workers = [...population.genoses].filter(
      (genos) =>
        !genos.isDead &&
        genos.workplaceBuildingNetworkId !== undefined &&
        genos.workplaceBuildingNetworkId !== null &&
        genos.workplaceBuildingNetworkId === building.networkId
    );
    const rows = workerRows(building, workers);
    return {
      kind: "workers",
      title: "Workers",
      groups: rows.length === 0 ? [] : [capRows(null, "", rows)],
      empty: rows.length === 0 ? "Nobody works here" : null
    };
  }

  return null;
}

/** Residents grouped by housing unit, ordered by unit id so groups never jump. */
function groupResidents(building: MapBuilding, residents: GenosStatus[]): OccupantGroup[] {
  const byUnit = new Map<number | null, GenosStatus[]>();
  for (const genos of residents) {
    const unitId = genos.housingUnitNetworkId === undefined ? null : genos.housingUnitNetworkId;
    const bucket = byUnit.get(unitId);
    if (bucket === undefined) byUnit.set(unitId, [genos]);
    else bucket.push(genos);
  }

  const unitIds = [...byUnit.keys()].sort((left, right) => {
    if (left === right) return 0;
    if (left === null) return 1;
    if (right === null) return -1;
    return left - right;
  });

  return unitIds.map((unitId) => {
    const members = byUnit.get(unitId) ?? [];
    members.sort((left, right) => left.networkId - right.networkId);
    const unitNumber = members
      .map((genos) => (genos.housingUnitNumber ?? "").trim())
      .find((number) => number !== "");
    const label =
      unitNumber !== undefined ? unitNumber : unitId !== null ? `Unit #${unitId}` : "No unit";
    return capRows(unitId, label, residentRows(building, members));
  });
}

function residentRows(building: MapBuilding, residents: GenosStatus[]): OccupantRow[] {
  return residents.map((genos) => ({
    networkId: genos.networkId,
    name: genos.name || `Genos #${genos.networkId}`,
    detail: "",
    present: isInsideFootprint(building, genos)
  }));
}

function workerRows(building: MapBuilding, workers: GenosStatus[]): OccupantRow[] {
  return workers
    .slice()
    .sort((left, right) => left.networkId - right.networkId)
    .map((genos) => ({
      networkId: genos.networkId,
      name: genos.name || `Genos #${genos.networkId}`,
      detail: workerDetail(genos),
      present: isInsideFootprint(building, genos)
    }));
}

/** A worker's role when the server sends one, otherwise what they are doing. */
function workerDetail(genos: GenosStatus): string {
  const position = (genos.jobPosition ?? "").trim();
  if (position !== "") return position;
  const action = (genos.currentAction ?? "").trim();
  return action !== "" ? action : "Idle";
}

function capRows(unitNetworkId: number | null, label: string, rows: OccupantRow[]): OccupantGroup {
  return {
    unitNetworkId,
    label,
    rows: rows.slice(0, OCCUPANT_LIST_LIMIT),
    hiddenCount: Math.max(0, rows.length - OCCUPANT_LIST_LIMIT)
  };
}
