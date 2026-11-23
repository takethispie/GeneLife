/**
 * Hub DTOs mirrored from `Genelife.Contracts`.
 *
 * SignalR payloads are not described by OpenAPI, so these types are maintained
 * by hand. The JSON fixtures under `src/test/fixtures` fail loudly when the
 * server contract changes shape.
 */

export type TileFlag = "Land" | "Road" | "Water" | "Occupied" | "Reserved";

export interface MapTileRun {
  x: number;
  y: number;
  length: number;
  /** Comma-separated flag list, for example `"Land, Road"`. */
  flags: string;
}

export interface MapBuilding {
  networkId: number;
  kind: string;
  name: string;
  district: string;
  x: number;
  y: number;
  width: number;
  height: number;
  entryX: number;
  entryY: number;
  ownerNetworkId?: number | null;
}

export interface MapSnapshot {
  mapId: string;
  version: number;
  seed: number;
  width: number;
  height: number;
  tileSize: number;
  minX: number;
  minY: number;
  maxX: number;
  maxY: number;
  tiles: MapTileRun[];
  buildings: MapBuilding[];
}

export interface MapDelta {
  mapId: string;
  fromVersion: number;
  toVersion: number;
  tick: number;
  added: MapBuilding[];
  updated: MapBuilding[];
  removed: number[];
}

export interface SimStatus {
  networkId: number;
  name: string;
  age: number;
  isPlayerControlled: boolean;
  hunger: number;
  energy: number;
  hygiene: number;
  bladder: number;
  currentAction: string;
  timeRemaining: number;
  x: number;
  y: number;
  tileX: number;
  tileY: number;
  hasMoveTarget: boolean;
  moveTargetX: number;
  moveTargetY: number;
  walletBalance: number;
  savingsBalance: number;
  unmetDemand: number;
  householdNetworkId?: number | null;
  housingUnitNetworkId?: number | null;
  tenure: string;
  housingTier: string;
  homeless: boolean;
  stage: string;
  health: number;
  topSkill: string;
  seniority: string;
  isDead: boolean;
}

export interface CompanyStatus {
  networkId: number;
  name: string;
  employeeCount: number;
  officeCount: number;
  balance: number;
  reservedForPayroll: number;
  arrears: number;
  revenueLastCycle: number;
  wagesLastCycle: number;
  solvency: string;
}

export interface WorldSnapshot {
  tick: number;
  version: number;
  sims: SimStatus[];
  companies: CompanyStatus[];
}

export interface WorldSnapshotDelta {
  tick: number;
  sinceTick: number;
  version: number;
  full: boolean;
  sims: SimStatus[];
  companies: CompanyStatus[];
  removedSims: number[];
  removedCompanies: number[];
}

export interface WelcomeMessage {
  playerId: string;
  simNetworkId: number;
  mapId: string;
  mapVersion: number;
  tick: number;
  tickIntervalMs: number;
  snapshotVersion: number;
  protocolVersion: number;
}

export interface IntentRejected {
  playerId: string;
  sequence: number;
  code: string;
  message: string;
}

export interface SnapshotRejected {
  error?: string;
  message?: string;
}

export interface SetViewportRequest {
  centerX: number;
  centerY: number;
  radius: number;
}

export interface ViewportResult {
  success: boolean;
  centerX: number;
  centerY: number;
  radius: number;
  error?: string | null;
}

export interface IntentResult {
  success: boolean;
  error?: string | null;
}

export interface MoveIntent {
  type: "move";
  playerId: string;
  sequence: number;
  simNetworkId: number;
  targetTileX: number;
  targetTileY: number;
}

export interface UseBuildingIntent {
  type: "useBuilding";
  playerId: string;
  sequence: number;
  simNetworkId: number;
  buildingNetworkId: number;
}

export type Intent = MoveIntent | UseBuildingIntent;
