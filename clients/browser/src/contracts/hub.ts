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

/**
 * Sent after an admin world reset. The client must drop its cached map and
 * world state, then request a fresh Welcome, map snapshot and world snapshot.
 */
export interface WorldResetMessage {
  mapId: string;
  mapVersion: number;
  layoutVersion: number;
  seed: number;
  tick: number;
}

export interface AppearanceStatus {
  skinTone: number;
  faceShape: number;
  hairColor: number;
  hairStyle: number;
  eyeColor: number;
  bodyFrame: number;
  heightScale: number;
  ageBand: number;
  expression: number;
  appearanceVersion: number;
}

export interface GenosStatus {
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
  appearance?: AppearanceStatus | null;
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
  genoses: GenosStatus[];
  companies: CompanyStatus[];
}

export interface WorldSnapshotDelta {
  tick: number;
  sinceTick: number;
  version: number;
  full: boolean;
  genoses: GenosStatus[];
  companies: CompanyStatus[];
  removedGenoses: number[];
  removedCompanies: number[];
}

export interface WelcomeMessage {
  playerId: string;
  genosNetworkId: number;
  mapId: string;
  mapVersion: number;
  tick: number;
  tickIntervalMs: number;
  snapshotVersion: number;
  protocolVersion: number;
  awaitingRespawn?: boolean;
  lastGenosName?: string | null;
  lastGenosAge?: number;
}

export type PlayerLifecycleKind = "died" | "respawned";

export interface PlayerLifecycleNotice {
  playerId: string;
  kind: PlayerLifecycleKind;
  genosNetworkId: number;
  previousGenosNetworkId: number;
  name: string | null;
  age: number;
  cause: string | null;
  tick: number;
  canRespawn: boolean;
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
  genosNetworkId: number;
  targetTileX: number;
  targetTileY: number;
}

export interface UseBuildingIntent {
  type: "useBuilding";
  playerId: string;
  sequence: number;
  genosNetworkId: number;
  buildingNetworkId: number;
}

export interface PlayerActionIntent {
  type: "playerAction";
  playerId: string;
  sequence: number;
  genosNetworkId: number;
  kind: string;
  payload: Record<string, unknown>;
}

export type Intent = MoveIntent | UseBuildingIntent | PlayerActionIntent;
export type ChatChannel = "Say" | "Whisper" | "Venue" | "System";

export interface ChatMessage {
  id: string;
  playerId: string;
  displayName: string | null;
  channel: ChatChannel | number;
  text: string;
  tick: number;
  targetPlayerId: string | null;
  venueNetworkId: number | null;
}

export interface ChatSendRequest {
  text: string;
  channel: ChatChannel;
  targetPlayerId?: string | null;
}

export interface ChatSendResult {
  accepted: boolean;
  code: string | null;
  notice: string | null;
  message: ChatMessage | null;
  recipients: string[];
}

export type EmoteKind = "Wave" | "Nod" | "ShakeHead" | "Sit" | "Stand" | "Cheer" | "Point";

export interface ChatEmote {
  id: string;
  playerId: string;
  displayName: string | null;
  emote: EmoteKind | number;
  tick: number;
  venueNetworkId: number | null;
}

export interface EmoteSendRequest {
  emote: number;
}

export interface EmoteSendResult {
  accepted: boolean;
  code: string | null;
  notice: string | null;
  emote: ChatEmote | null;
  recipients: string[];
}
