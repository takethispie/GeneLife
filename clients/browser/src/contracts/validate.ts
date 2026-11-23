import type {
  AppearanceStatus,
  CompanyStatus,
  Intent,
  IntentRejected,
  MapBuilding,
  MapDelta,
  MapSnapshot,
  MapTileRun,
  SimStatus,
  WelcomeMessage,
  WorldSnapshot,
  WorldSnapshotDelta
} from "./hub";

export class ContractError extends Error {
  constructor(
    message: string,
    readonly path: string
  ) {
    super(`${path}: ${message}`);
    this.name = "ContractError";
  }
}

type JsonRecord = Record<string, unknown>;

function asRecord(value: unknown, path: string): JsonRecord {
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    throw new ContractError("expected an object", path);
  }
  return value as JsonRecord;
}

function requireString(record: JsonRecord, key: string, path: string): string {
  const value = record[key];
  if (typeof value !== "string") {
    throw new ContractError(`expected a string for "${key}"`, path);
  }
  return value;
}

function optionalString(record: JsonRecord, key: string, path: string): string {
  const value = record[key];
  if (value === undefined || value === null) return "";
  if (typeof value !== "string") {
    throw new ContractError(`expected a string for "${key}"`, path);
  }
  return value;
}

function requireNumber(record: JsonRecord, key: string, path: string): number {
  const value = record[key];
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new ContractError(`expected a finite number for "${key}"`, path);
  }
  return value;
}

function optionalNumber(record: JsonRecord, key: string, path: string): number {
  const value = record[key];
  if (value === undefined || value === null) return 0;
  if (typeof value !== "number" || !Number.isFinite(value)) {
    throw new ContractError(`expected a finite number for "${key}"`, path);
  }
  return value;
}

function requireBoolean(record: JsonRecord, key: string, path: string): boolean {
  const value = record[key];
  if (typeof value !== "boolean") {
    throw new ContractError(`expected a boolean for "${key}"`, path);
  }
  return value;
}

function optionalBoolean(record: JsonRecord, key: string, path: string): boolean {
  const value = record[key];
  if (value === undefined || value === null) return false;
  if (typeof value !== "boolean") {
    throw new ContractError(`expected a boolean for "${key}"`, path);
  }
  return value;
}

function requireArray(record: JsonRecord, key: string, path: string): unknown[] {
  const value = record[key];
  if (!Array.isArray(value)) {
    throw new ContractError(`expected an array for "${key}"`, path);
  }
  return value;
}

function optionalArray(record: JsonRecord, key: string, path: string): unknown[] {
  const value = record[key];
  if (value === undefined || value === null) return [];
  if (!Array.isArray(value)) {
    throw new ContractError(`expected an array for "${key}"`, path);
  }
  return value;
}

function requireInteger(record: JsonRecord, key: string, path: string): number {
  const value = requireNumber(record, key, path);
  if (!Number.isInteger(value)) {
    throw new ContractError(`expected an integer for "${key}"`, path);
  }
  return value;
}

function validateTileRun(value: unknown, path: string): MapTileRun {
  const record = asRecord(value, path);
  return {
    x: requireInteger(record, "x", path),
    y: requireInteger(record, "y", path),
    length: requireInteger(record, "length", path),
    flags: requireString(record, "flags", path)
  };
}

export function validateMapBuilding(value: unknown, path = "mapBuilding"): MapBuilding {
  const record = asRecord(value, path);
  const building: MapBuilding = {
    networkId: requireInteger(record, "networkId", path),
    kind: requireString(record, "kind", path),
    name: requireString(record, "name", path),
    district: optionalString(record, "district", path),
    x: requireInteger(record, "x", path),
    y: requireInteger(record, "y", path),
    width: requireInteger(record, "width", path),
    height: requireInteger(record, "height", path),
    entryX: requireInteger(record, "entryX", path),
    entryY: requireInteger(record, "entryY", path)
  };

  const owner = record.ownerNetworkId;
  if (typeof owner === "number" && Number.isFinite(owner)) {
    building.ownerNetworkId = owner;
  } else if (owner !== undefined && owner !== null) {
    throw new ContractError('expected a number or null for "ownerNetworkId"', path);
  }

  return building;
}

export function validateMapSnapshot(value: unknown, path = "mapSnapshot"): MapSnapshot {
  const record = asRecord(value, path);
  const snapshot: MapSnapshot = {
    mapId: requireString(record, "mapId", path),
    version: requireInteger(record, "version", path),
    seed: requireInteger(record, "seed", path),
    width: requireInteger(record, "width", path),
    height: requireInteger(record, "height", path),
    tileSize: requireNumber(record, "tileSize", path),
    minX: requireInteger(record, "minX", path),
    minY: requireInteger(record, "minY", path),
    maxX: requireInteger(record, "maxX", path),
    maxY: requireInteger(record, "maxY", path),
    tiles: requireArray(record, "tiles", path).map((run, index) =>
      validateTileRun(run, `${path}.tiles[${index}]`)
    ),
    buildings: requireArray(record, "buildings", path).map((building, index) =>
      validateMapBuilding(building, `${path}.buildings[${index}]`)
    )
  };

  if (snapshot.width <= 0 || snapshot.height <= 0) {
    throw new ContractError("width and height must be positive", path);
  }
  if (snapshot.tileSize <= 0) {
    throw new ContractError("tileSize must be positive", path);
  }
  if (snapshot.maxX < snapshot.minX || snapshot.maxY < snapshot.minY) {
    throw new ContractError("bounds must satisfy max >= min", path);
  }
  if (snapshot.maxX - snapshot.minX + 1 !== snapshot.width) {
    throw new ContractError("bounds width does not match width", path);
  }
  if (snapshot.maxY - snapshot.minY + 1 !== snapshot.height) {
    throw new ContractError("bounds height does not match height", path);
  }

  return snapshot;
}

export function validateSim(value: unknown, path = "sim"): SimStatus {
  const record = asRecord(value, path);
  return {
    networkId: requireInteger(record, "networkId", path),
    name: requireString(record, "name", path),
    age: requireInteger(record, "age", path),
    isPlayerControlled: requireBoolean(record, "isPlayerControlled", path),
    hunger: optionalNumber(record, "hunger", path),
    energy: optionalNumber(record, "energy", path),
    hygiene: optionalNumber(record, "hygiene", path),
    bladder: optionalNumber(record, "bladder", path),
    currentAction: optionalString(record, "currentAction", path),
    timeRemaining: optionalNumber(record, "timeRemaining", path),
    x: requireNumber(record, "x", path),
    y: requireNumber(record, "y", path),
    tileX: requireInteger(record, "tileX", path),
    tileY: requireInteger(record, "tileY", path),
    hasMoveTarget: optionalBoolean(record, "hasMoveTarget", path),
    moveTargetX: optionalNumber(record, "moveTargetX", path),
    moveTargetY: optionalNumber(record, "moveTargetY", path),
    walletBalance: optionalNumber(record, "walletBalance", path),
    savingsBalance: optionalNumber(record, "savingsBalance", path),
    unmetDemand: optionalNumber(record, "unmetDemand", path),
    tenure: optionalString(record, "tenure", path),
    housingTier: optionalString(record, "housingTier", path),
    homeless: optionalBoolean(record, "homeless", path),
    stage: optionalString(record, "stage", path),
    health: optionalNumber(record, "health", path),
    topSkill: optionalString(record, "topSkill", path),
    seniority: optionalString(record, "seniority", path),
    isDead: optionalBoolean(record, "isDead", path),
    appearance: validateAppearance(record.appearance, `${path}.appearance`)
  };
}

export function validateAppearance(value: unknown, path = "appearance"): AppearanceStatus | null {
  if (value === undefined || value === null) return null;
  const record = asRecord(value, path);
  return {
    skinTone: requireByte(record, "skinTone", path, 6),
    faceShape: requireByte(record, "faceShape", path, 5),
    hairColor: requireByte(record, "hairColor", path, 5),
    hairStyle: requireByte(record, "hairStyle", path, 7),
    eyeColor: requireByte(record, "eyeColor", path, 5),
    bodyFrame: requireByte(record, "bodyFrame", path, 3),
    heightScale: requireByte(record, "heightScale", path, 101),
    ageBand: requireByte(record, "ageBand", path, 5),
    expression: requireByte(record, "expression", path, 4),
    appearanceVersion: requireByte(record, "appearanceVersion", path, 256)
  };
}

function requireByte(record: Record<string, unknown>, key: string, path: string, maxExclusive: number): number {
  const value = record[key];
  if (typeof value !== "number" || !Number.isInteger(value) || value < 0 || value >= maxExclusive) {
    throw new ContractError(`${key} must be an integer in 0..${maxExclusive - 1}`, `${path}.${key}`);
  }
  return value;
}

export function validateCompany(value: unknown, path = "company"): CompanyStatus {
  const record = asRecord(value, path);
  return {
    networkId: requireInteger(record, "networkId", path),
    name: requireString(record, "name", path),
    employeeCount: requireInteger(record, "employeeCount", path),
    officeCount: optionalNumber(record, "officeCount", path),
    balance: optionalNumber(record, "balance", path),
    reservedForPayroll: optionalNumber(record, "reservedForPayroll", path),
    arrears: optionalNumber(record, "arrears", path),
    revenueLastCycle: optionalNumber(record, "revenueLastCycle", path),
    wagesLastCycle: optionalNumber(record, "wagesLastCycle", path),
    solvency: optionalString(record, "solvency", path)
  };
}

export function validateWorldSnapshot(value: unknown, path = "worldSnapshot"): WorldSnapshot {
  const record = asRecord(value, path);
  return {
    tick: requireInteger(record, "tick", path),
    version: requireInteger(record, "version", path),
    sims: requireArray(record, "sims", path).map((sim, index) =>
      validateSim(sim, `${path}.sims[${index}]`)
    ),
    companies: requireArray(record, "companies", path).map((company, index) =>
      validateCompany(company, `${path}.companies[${index}]`)
    )
  };
}

export function validateWorldSnapshotDelta(
  value: unknown,
  path = "worldSnapshotDelta"
): WorldSnapshotDelta {
  const record = asRecord(value, path);
  const delta: WorldSnapshotDelta = {
    tick: requireInteger(record, "tick", path),
    sinceTick: requireInteger(record, "sinceTick", path),
    version: requireInteger(record, "version", path),
    full: requireBoolean(record, "full", path),
    sims: requireArray(record, "sims", path).map((sim, index) =>
      validateSim(sim, `${path}.sims[${index}]`)
    ),
    companies: optionalArray(record, "companies", path).map((company, index) =>
      validateCompany(company, `${path}.companies[${index}]`)
    ),
    removedSims: optionalArray(record, "removedSims", path).map((id, index) => {
      if (typeof id !== "number" || !Number.isFinite(id)) {
        throw new ContractError("expected numeric ids", `${path}.removedSims[${index}]`);
      }
      return id;
    }),
    removedCompanies: optionalArray(record, "removedCompanies", path).map((id, index) => {
      if (typeof id !== "number" || !Number.isFinite(id)) {
        throw new ContractError("expected numeric ids", `${path}.removedCompanies[${index}]`);
      }
      return id;
    })
  };

  return delta;
}

export function validateMapDelta(value: unknown, path = "mapDelta"): MapDelta {
  const record = asRecord(value, path);
  return {
    mapId: requireString(record, "mapId", path),
    fromVersion: requireInteger(record, "fromVersion", path),
    toVersion: requireInteger(record, "toVersion", path),
    tick: requireInteger(record, "tick", path),
    added: optionalArray(record, "added", path).map((building, index) =>
      validateMapBuilding(building, `${path}.added[${index}]`)
    ),
    updated: optionalArray(record, "updated", path).map((building, index) =>
      validateMapBuilding(building, `${path}.updated[${index}]`)
    ),
    removed: optionalArray(record, "removed", path).map((id, index) => {
      if (typeof id !== "number" || !Number.isFinite(id)) {
        throw new ContractError("expected numeric ids", `${path}.removed[${index}]`);
      }
      return id;
    })
  };
}

export function validateWelcome(value: unknown, path = "welcome"): WelcomeMessage {
  const record = asRecord(value, path);
  const welcome: WelcomeMessage = {
    playerId: requireString(record, "playerId", path),
    simNetworkId: requireInteger(record, "simNetworkId", path),
    mapId: requireString(record, "mapId", path),
    mapVersion: requireInteger(record, "mapVersion", path),
    tick: requireInteger(record, "tick", path),
    tickIntervalMs: requireInteger(record, "tickIntervalMs", path),
    snapshotVersion: requireInteger(record, "snapshotVersion", path),
    protocolVersion: requireInteger(record, "protocolVersion", path)
  };

  if (welcome.protocolVersion < 1) {
    throw new ContractError("unsupported protocol version", path);
  }
  if (welcome.tickIntervalMs <= 0) {
    throw new ContractError("tickIntervalMs must be positive", path);
  }

  return welcome;
}

export function validateIntentRejected(value: unknown, path = "intentRejected"): IntentRejected {
  const record = asRecord(value, path);
  return {
    playerId: requireString(record, "playerId", path),
    sequence: requireInteger(record, "sequence", path),
    code: requireString(record, "code", path),
    message: optionalString(record, "message", path)
  };
}

/**
 * Parses a polymorphic intent by its `type` discriminator. Unknown fields are
 * tolerated; unknown discriminators fail loudly.
 */
export function parseIntent(value: unknown, path = "intent"): Intent {
  const record = asRecord(value, path);
  const type = requireString(record, "type", path);
  const playerId = requireString(record, "playerId", path);
  const sequence = optionalNumber(record, "sequence", path);

  if (type === "move") {
    return {
      type,
      playerId,
      sequence,
      simNetworkId: optionalNumber(record, "simNetworkId", path),
      targetTileX: requireInteger(record, "targetTileX", path),
      targetTileY: requireInteger(record, "targetTileY", path)
    };
  }

  if (type === "useBuilding") {
    return {
      type,
      playerId,
      sequence,
      simNetworkId: optionalNumber(record, "simNetworkId", path),
      buildingNetworkId: requireInteger(record, "buildingNetworkId", path)
    };
  }

  throw new ContractError(`unknown intent discriminator "${type}"`, path);
}
