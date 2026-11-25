import {
  HubConnectionBuilder,
  LogLevel,
  type HubConnection
} from "@microsoft/signalr";
import type {
  ChatEmote,
  ChatMessage,
  ChatSendRequest,
  ChatSendResult,
  EmoteSendResult,
  Intent,
  IntentResult,
  SetViewportRequest,
  ViewportResult
} from "../contracts/hub";
import {
  validateIntentRejected,
  validateMapDelta,
  validateMapSnapshot,
  validatePlayerLifecycleNotice,
  validateWelcome,
  validateWorldReset,
  validateWorldSnapshot,
  validateWorldSnapshotDelta
} from "../contracts/validate";
import {
  HUB_EVENTS,
  HUB_METHODS,
  type ConnectionState,
  type HubHandlers
} from "./events";

export interface HubLogger {
  debug(message: string): void;
  warn(message: string): void;
  error(message: string): void;
}

const CHAT_CHANNEL_VALUES: Record<string, number> = {
  Say: 0,
  Whisper: 1,
  Venue: 2,
  System: 3
};

export class HubNotConnectedError extends Error {
  constructor() {
    super("The world connection is not open");
    this.name = "HubNotConnectedError";
  }
}

export function buildHubUrl(hubUrl: string, token: string): string {
  const url = new URL(hubUrl);
  url.searchParams.set("access_token", token);
  return url.toString();
}

export interface WorldHubClientOptions {
  logger?: HubLogger;
  reconnectDelaysMs?: number[];
  /** Injectable for tests; defaults to a real SignalR connection. */
  connectionFactory?: (url: string) => HubConnection;
}

/**
 * Owns exactly one SignalR connection per player session. All server events are
 * registered before `start()`, so the initial map/snapshot cannot race past a
 * missing handler.
 */
export class WorldHubClient {
  private connection: HubConnection | null = null;
  private state: ConnectionState = "idle";
  private reconnectCount = 0;
  private handlersAttached = false;

  constructor(
    private readonly hubUrl: string,
    private readonly handlers: HubHandlers,
    private readonly options: WorldHubClientOptions = {}
  ) {}

  get currentState(): ConnectionState {
    return this.state;
  }

  get reconnects(): number {
    return this.reconnectCount;
  }

  async connect(token: string): Promise<void> {
    if (this.connection !== null) {
      throw new Error("A hub connection already exists; stop it before connecting again");
    }

    this.setState("connecting");
    const connection = this.options.connectionFactory
      ? this.options.connectionFactory(buildHubUrl(this.hubUrl, token))
      : this.createConnection(buildHubUrl(this.hubUrl, token));

    this.connection = connection;
    this.attachHandlers(connection);

    try {
      await connection.start();
      this.setState("connected");
      this.log("debug", "world hub connected");
    } catch (error) {
      this.setState("failed");
      this.log(
        "error",
        `world hub failed to start: ${error instanceof Error ? error.message : "unknown error"}`
      );
      await this.disposeConnection();
      throw error;
    }
  }

  async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    this.setState("closed");
    if (connection !== null) {
      this.detachHandlers(connection);
      try {
        await connection.stop();
      } catch {
        // Closing an already-failed connection is not an error for the caller.
      }
    }
  }

  async requestSnapshot(sinceTick: number | null): Promise<void> {
    await this.requireConnection().invoke(HUB_METHODS.requestSnapshot, sinceTick);
  }

  async requestWelcome(): Promise<void> {
    await this.requireConnection().send(HUB_METHODS.requestWelcome);
  }

  async requestMapSnapshot(): Promise<void> {
    await this.requireConnection().send(HUB_METHODS.requestMapSnapshot);
  }

  async acknowledgeSnapshot(tick: number): Promise<void> {
    await this.requireConnection().send(HUB_METHODS.acknowledgeSnapshot, tick);
  }

  async setViewport(request: SetViewportRequest): Promise<ViewportResult> {
    return this.requireConnection().invoke<ViewportResult>(HUB_METHODS.setViewport, request);
  }

  async clearViewport(): Promise<void> {
    await this.requireConnection().send(HUB_METHODS.clearViewport);
  }

  async submitIntent(intent: Intent): Promise<IntentResult> {
    return this.requireConnection().invoke<IntentResult>(HUB_METHODS.submitIntent, intent);
  }

  async sendChat(request: ChatSendRequest): Promise<ChatSendResult> {
    return this.requireConnection().invoke<ChatSendResult>(HUB_METHODS.sendChat, {
      text: request.text,
      channel: CHAT_CHANNEL_VALUES[request.channel],
      targetPlayerId: request.targetPlayerId ?? null
    });
  }

  async sendEmote(emote: number): Promise<EmoteSendResult> {
    return this.requireConnection().invoke<EmoteSendResult>(HUB_METHODS.sendEmote, { emote });
  }

  private requireConnection(): HubConnection {
    if (this.connection === null || this.state !== "connected") {
      throw new HubNotConnectedError();
    }
    return this.connection;
  }

  private createConnection(url: string): HubConnection {
    return new HubConnectionBuilder()
      .withUrl(url)
      .withAutomaticReconnect(
        this.options.reconnectDelaysMs ?? [0, 1_000, 2_000, 5_000, 10_000, 20_000]
      )
      .configureLogging(LogLevel.Warning)
      .build();
  }

  private attachHandlers(connection: HubConnection): void {
    if (this.handlersAttached) return;
    this.handlersAttached = true;

    connection.on(HUB_EVENTS.welcome, (raw: unknown) => {
      this.guard("Welcome", () => this.handlers.onWelcome(validateWelcome(raw)));
    });
    connection.on(HUB_EVENTS.mapSnapshot, (raw: unknown) => {
      this.guard("SendMapSnapshot", () => this.handlers.onMapSnapshot(validateMapSnapshot(raw)));
    });
    connection.on(HUB_EVENTS.mapDelta, (raw: unknown) => {
      this.guard("SendMapDelta", () => this.handlers.onMapDelta(validateMapDelta(raw)));
    });
    connection.on(HUB_EVENTS.snapshot, (raw: unknown) => {
      this.guard("SendSnapshot", () => this.handlers.onSnapshot(validateWorldSnapshot(raw)));
    });
    connection.on(HUB_EVENTS.snapshotDelta, (raw: unknown) => {
      this.guard("SendSnapshotDelta", () =>
        this.handlers.onSnapshotDelta(validateWorldSnapshotDelta(raw))
      );
    });
    connection.on(HUB_EVENTS.snapshotRejected, (raw: unknown) => {
      const record =
        raw !== null && typeof raw === "object" ? (raw as Record<string, unknown>) : {};
      this.handlers.onSnapshotRejected({
        error: typeof record.error === "string" ? record.error : undefined,
        message: typeof record.message === "string" ? record.message : undefined
      });
    });
    connection.on(HUB_EVENTS.intentRejected, (raw: unknown) => {
      this.handlers.onIntentRejected(validateIntentRejected(raw));
    });
    connection.on(HUB_EVENTS.playerLifecycle, (raw: unknown) => {
      this.guard("PlayerLifecycle", () =>
        this.handlers.onPlayerLifecycle?.(validatePlayerLifecycleNotice(raw))
      );
    });
    connection.on(HUB_EVENTS.worldReset, (raw: unknown) => {
      this.guard("WorldReset", () =>
        this.handlers.onWorldReset?.(validateWorldReset(raw))
      );
    });
    connection.on(HUB_EVENTS.receiveChat, (raw: unknown) => {
      this.guard("ReceiveChat", () => this.handlers.onChatMessage?.(raw as ChatMessage));
    });
    connection.on(HUB_EVENTS.receiveEmote, (raw: unknown) => {
      this.guard("ReceiveEmote", () => this.handlers.onEmote?.(raw as ChatEmote));
    });

    connection.onreconnecting(() => {
      this.reconnectCount++;
      this.setState("reconnecting");
    });
    connection.onreconnected(() => {
      this.setState("connected");
      this.handlers.onReconnected?.();
    });
    connection.onclose(() => {
      this.setState(this.state === "closed" ? "closed" : "failed");
    });
  }

  private detachHandlers(connection: HubConnection): void {
    if (!this.handlersAttached) return;
    this.handlersAttached = false;
    for (const eventName of Object.values(HUB_EVENTS)) connection.off(eventName);
  }

  private async disposeConnection(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    if (connection !== null) {
      this.detachHandlers(connection);
      try {
        await connection.stop();
      } catch {
        // Ignore cleanup failures.
      }
    }
  }

  private guard(event: string, action: () => void): void {
    try {
      action();
    } catch (error) {
      this.log(
        "error",
        `${event} payload rejected: ${error instanceof Error ? error.message : "invalid payload"}`
      );
    }
  }

  private setState(state: ConnectionState): void {
    if (this.state === state) return;
    this.state = state;
    this.handlers.onStateChange(state);
  }

  private log(level: keyof HubLogger, message: string): void {
    this.options.logger?.[level](message);
  }
}
