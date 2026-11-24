import type {
  ChatEmote,
  ChatMessage,
  Intent,
  IntentRejected,
  IntentResult,
  MapDelta,
  MapSnapshot,
  PlayerLifecycleNotice,
  SetViewportRequest,
  SnapshotRejected,
  ViewportResult,
  WelcomeMessage,
  WorldSnapshot,
  WorldSnapshotDelta
} from "../contracts/hub";

export const HUB_EVENTS = {
  welcome: "Welcome",
  mapSnapshot: "SendMapSnapshot",
  mapDelta: "SendMapDelta",
  snapshot: "SendSnapshot",
  snapshotDelta: "SendSnapshotDelta",
  snapshotRejected: "SnapshotRejected",
  intentRejected: "IntentRejected",
  playerLifecycle: "PlayerLifecycle",
  receiveChat: "ReceiveChat",
  receiveEmote: "ReceiveEmote"
} as const;

export const HUB_METHODS = {
  requestSnapshot: "RequestSnapshot",
  acknowledgeSnapshot: "AcknowledgeSnapshot",
  requestMapSnapshot: "RequestMapSnapshot",
  setViewport: "SetViewport",
  clearViewport: "ClearViewport",
  submitIntent: "SubmitIntent",
  sendChat: "SendChat",
  sendEmote: "SendEmote"
} as const;

export type ConnectionState =
  | "idle"
  | "connecting"
  | "connected"
  | "reconnecting"
  | "failed"
  | "closed";

export interface HubHandlers {
  onWelcome(message: WelcomeMessage): void;
  onMapSnapshot(snapshot: MapSnapshot): void;
  onMapDelta(delta: MapDelta): void;
  onSnapshot(snapshot: WorldSnapshot): void;
  onSnapshotDelta(delta: WorldSnapshotDelta): void;
  onSnapshotRejected(rejection: SnapshotRejected): void;
  onIntentRejected(rejection: IntentRejected): void;
  onPlayerLifecycle?(notice: PlayerLifecycleNotice): void;
  onStateChange(state: ConnectionState): void;
  onReconnected?(): void;
  onChatMessage?(message: ChatMessage): void;
  onEmote?(emote: ChatEmote): void;
}

export type HubInvocation = {
  setViewport(request: SetViewportRequest): Promise<ViewportResult>;
  submitIntent(intent: Intent): Promise<IntentResult>;
};
