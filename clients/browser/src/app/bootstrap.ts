import { ASSET_MANIFEST, preloadAssets } from "../assets/manifest";
import { SessionController } from "../auth/session";
import { buildInfo } from "../config/build-info";
import { GAME_CONSTANTS } from "../config/constants";
import type { ClientConfig } from "../config/env";
import type { WelcomeMessage } from "../contracts/hub";
import {
  validateMapSnapshot,
  validateWorldSnapshot,
  validateWelcome
} from "../contracts/validate";
import { BuildingSelectionController } from "../input/building-selection";
import { TouchInput } from "../input/touch";
import { CameraInput } from "../input/camera-input";
import { TargetingController } from "../input/targeting";
import { WorldHubClient, type HubLogger } from "../net/hub-connection";
import type { HubHandlers } from "../net/events";
import { IntentClient } from "../net/intent-client";
import { LatencyRecorder } from "../net/latency";
import { recoverSession } from "../net/recovery";
import { TrafficMeter } from "../net/traffic";
import {
  visibleRectToViewport,
  ViewportSynchronizer,
  type TileRect
} from "../net/viewport-client";
import { Camera } from "../render/camera";
import { DebugOverlay } from "../render/debug-overlay";
import { PixiScene } from "../render/pixi-app";
import { SceneRenderer, type SceneStats } from "../render/render-loop";
import { SimulationStore } from "../simulation/store";
import { AudioCuePlayer, type AudioCue } from "../ui/audio/audio";
import { Hud } from "../ui/hud";
import { HudDock } from "../ui/hud-dock";
import { NotificationCenter, type NotificationKind } from "../ui/notifications";
import { RespawnPopin, type RespawnPopinResult } from "../ui/respawn-popin";
import { SettingsStore } from "../ui/settings";
import { UiStore } from "../ui/ui-store";
import { ChatPanel } from "../ui/social/chat";
import { PlayerDirectoryPanel } from "../ui/social/directory";
import { RelationshipsPanel } from "../ui/social/relationships";
import { VenuePanel } from "../ui/social/venue";
import { LoadingScreen } from "./loading";

export interface ClientRuntime {
  connect(playerId: string): Promise<void>;
  signOut(): Promise<void>;
  retry(): Promise<void>;
  dispose(): void;
}

export interface StartClientOptions {
  root: HTMLElement;
  loading: LoadingScreen;
  config: ClientConfig;
  fetchImpl?: typeof fetch;
}

export async function startClient(options: StartClientOptions): Promise<ClientRuntime> {
  const { root, loading, config } = options;
  const fetchImpl = options.fetchImpl ?? fetch;
  const info = buildInfo();
  const useFixtures = config.useFixtures || isFixtureRequested();

  loading.setStep("config", "done");

  const playerId = resolvePlayerId(config);

  loading.setStep("assets", "active");
  const assets = await preloadAssets(ASSET_MANIFEST, fetchImpl);
  loading.setStep(
    "assets",
    "done",
    assets.missing.length === 0 ? undefined : "procedural fallback for missing art"
  );

  loading.setStep("scene", "active");
  const pixi = new PixiScene();
  await pixi.init(root);

  const hudRoot = document.createElement("div");
  hudRoot.id = "hud";
  root.appendChild(hudRoot);
  const dock = new HudDock(hudRoot);

  const store = new SimulationStore();
  const ui = new UiStore();
  const notifications = new NotificationCenter({ dedupeWindowMs: GAME_CONSTANTS.notificationDedupeMs });
  const settings = new SettingsStore();
  const debugInfo = new DebugOverlay(root);
  settings.load();
  settings.applyToDocument();

  const audio = new AudioCuePlayer({ getSettings: () => settings.settings });
  const unlockAudio = (): void => {
    audio.unlock();
  };
  window.addEventListener("pointerdown", unlockAudio, { once: true, capture: true });
  window.addEventListener("keydown", unlockAudio, { once: true, capture: true });
  let lastCueNotificationId = 0;
  const unsubscribeAudioNotifications = notifications.subscribe((items) => {
    for (const item of items) {
      if (item.id <= lastCueNotificationId) continue;
      lastCueNotificationId = item.id;
      audio.play(notificationCue(item.kind, item.code));
    }
  });

  const camera = new Camera({ reducedMotion: settings.prefersReducedMotion() });
  const session = new SessionController({ apiBaseUrl: config.apiBaseUrl });
  const traffic = new TrafficMeter();

  ui.patch({
    playerId,
    buildStamp: info.stamp,
    protocolVersion: info.protocolVersion
  });

  let hub: WorldHubClient | null = null;
  let renderer: SceneRenderer | null = null;
  let lastAppliedAt = 0;
  let centeredOnPlayer = false;
  let sceneReady = false;

  const viewportSync = new ViewportSynchronizer({
    setViewport: async (request) => {
      if (hub === null || hub.currentState !== "connected") {
        return { success: true, ...request };
      }
      return hub.setViewport(request);
    },
    onResult: (result) => {
      ui.patch({ viewportRadius: result.radius });
    },
    onError: (message) => {
      notify("warning", "viewport_rejected", message);
    },
    debounceMs: GAME_CONSTANTS.viewportDebounceMs,
    changeThreshold: GAME_CONSTANTS.viewportChangeThreshold
  });

  const latency = new LatencyRecorder();
  const intent = new IntentClient({
    invoke: (payload) => {
      if (hub === null || useFixtures) {
        return Promise.reject(new Error("Fixture mode has no intent endpoint"));
      }
      return hub.submitIntent(payload);
    },
    playerId: () => {
      const state = session.store.state;
      if (state.status === "authenticated") return state.playerId;
      return useFixtures ? playerId : null;
    },
    ownGenosNetworkId: () => store.ownGenosId,
    onResult: (sent, result) => {
      if (!result.success) {
        notify("error", "intent_admission", result.error ?? "Action not accepted");
      } else if (sent.type === "useBuilding") {
        notify("info", "intent_admitted", "Request sent");
      }
    },
    onRejected: (code, message) => {
      notify("error", `intent_${code}`, rejectionMessage(code, message));
    },
    onLatency: (_sent, sentAt, ackAt) => {
      latency.record(sentAt, ackAt);
      const stats = latency.stats();
      ui.patch({ inputAckP50Ms: stats.p50Ms, inputAckP95Ms: stats.p95Ms });
    },
    minIntervalMs: GAME_CONSTANTS.localIntentIntervalMs
  });

  const selection = new BuildingSelectionController({
    getMap: () => store.map,
    getOwnGenosTile: () => {
      const genos = store.genoses.get(store.ownGenosId);
      return genos === undefined ? null : { x: genos.tileX, y: genos.tileY };
    },
    intent,
    onChanged: (building) => {
      renderer?.setSelectedBuilding(building);
      ui.patch({ selection: building === null ? null : { kind: "building", building } });
    },
    notify
  });

  const targeting = new TargetingController({
    canvas: pixi.app.canvas,
    camera,
    getMap: () => store.map,
    intent,
    onHover: (tile, walkable) => renderer?.setHover(tile, walkable),
    onTarget: (tile) => renderer?.setTarget(tile),
    onSelectBuilding: (building) => selection.select(building),
    notify
  });

  const touchInput = new TouchInput({
    canvas: pixi.app.canvas,
    camera,
    getEnabled: () => settings.settings.touchControls
  });

  const cameraInput = new CameraInput(
    {
      canvas: pixi.app.canvas,
      camera,
      getSettings: () => settings.settings,
      onCancel: () => targeting.clearAll(),
      onToggleDebug: () => settings.update({ debugOverlay: !settings.settings.debugOverlay })
    },
    (callback) => pixi.addFrameCallback(callback)
  );

  renderer = new SceneRenderer({
    pixi,
    store,
    camera,
    getSettings: () => settings.settings,
    onStats: (stats) => onSceneStats(stats),
    onViewportRect: (rect) => onViewportRect(rect),
    onMapChanged: () => {
      viewportSync.reset();
      selection.reconcile();
      ui.patch({
        mapId: store.map?.mapId ?? "",
        mapVersion: store.map?.version ?? 0
      });
    }
  });
  renderer.init();
  renderer.setTickInterval(40);
  renderer.setInterpolationDelay(
    settings.settings.reduceMotion ? 0 : GAME_CONSTANTS.defaultInterpolationDelayMs
  );

  const hud = new Hud({
    root: hudRoot,
    dock,
    ui,
    notifications,
    settings,
    actions: {
      onInteract: () => void selection.interact(),
      onSignOut: () => void runtime.signOut(),
      onRetry: () => void runtime.retry(),
      onSelectBuilding: (building) => selection.select(building),
      focusCanvas: () => pixi.app.canvas.focus()
    }
  });

  const respawnPopin = new RespawnPopin({
    root: hudRoot,
    onRespawn: () => requestRespawn()
  });

  const playerDirectory = new PlayerDirectoryPanel({
    root,
    apiBaseUrl: config.apiBaseUrl,
    token: () => session.store.token,
    dock,
    onError: (message) => notify("warning", "presence_failed", message)
  });

  const chatPanel = new ChatPanel({
    root,
    apiBaseUrl: config.apiBaseUrl,
    token: () => session.store.token,
    dock,
    sendChat: async (request) => {
      if (hub === null) {
        return {
          accepted: false,
          code: "not_connected",
          notice: "Connect to the world before chatting.",
          message: null,
          recipients: []
        };
      }
      return hub.sendChat(request);
    },
    sendEmote: async (emote) => {
      if (hub === null) {
        return {
          accepted: false,
          code: "not_connected",
          notice: "Connect to the world before using emotes.",
          emote: null,
          recipients: []
        };
      }
      return hub.sendEmote(emote);
    },
    onError: (message) => notify("warning", "chat_failed", message)
  });

  const relationshipsPanel = new RelationshipsPanel({
    root,
    apiBaseUrl: config.apiBaseUrl,
    token: () => session.store.token,
    dock,
    sendAction: async (kind, payload) => {
      const outcome = await intent.playerAction(kind, payload);
      return {
        status: outcome.status,
        message: "message" in outcome ? outcome.message : undefined
      };
    },
    onError: (message) => notify("warning", "relationships_failed", message)
  });

  const venuePanel = new VenuePanel({
    root,
    apiBaseUrl: config.apiBaseUrl,
    token: () => session.store.token,
    dock,
    sendAction: async (kind, payload) => {
      const outcome = await intent.playerAction(kind, payload);
      return {
        status: outcome.status,
        message: "message" in outcome ? outcome.message : undefined
      };
    },
    onError: (message) => notify("warning", "venue_failed", message)
  });

  const settingsUnsubscribe = settings.subscribe((next) => {
    settings.applyToDocument();
    audio.applySettings();
    camera.setReducedMotion(settings.prefersReducedMotion());
    renderer?.setInterpolationDelay(
      settings.prefersReducedMotion() ? 0 : GAME_CONSTANTS.defaultInterpolationDelayMs
    );
    debugInfo.setVisible(next.debugOverlay);
    if (next.debugOverlay) debugInfo.update(debugMetrics());
  });
  debugInfo.setVisible(settings.settings.debugOverlay);

  let disposed = false;

  const logger: HubLogger = {
    debug: (message) => {
      if (config.logLevel === "debug") console.debug(`[hub] ${message}`);
    },
    warn: (message) => console.warn(`[hub] ${message}`),
    error: (message) => console.error(`[hub] ${message}`)
  };

  const handlers: HubHandlers = {
    onWelcome: (welcome) => {
      traffic.addPayload(welcome);
      onWelcome(welcome);
    },
    onMapSnapshot: (snapshot) => {
      traffic.addPayload(snapshot);
      try {
        store.applyMapSnapshot(snapshot);
      } catch (error) {
        notify("error", "map_invalid", error instanceof Error ? error.message : "Map rejected");
        return;
      }
      const decoded = store.map;
      if (decoded !== null) camera.setMap(decoded);
      ui.patch({ mapId: snapshot.mapId, mapVersion: snapshot.version });
      selection.reconcile();
      sceneReady = true;
      loading.setStep("scene", "done");
      loading.hide();
    },
    onMapDelta: (delta) => {
      traffic.addPayload(delta);
      const outcome = store.applyMapDelta(delta);
      if (outcome.status === "applied") {
        ui.patch({ mapVersion: outcome.map.version });
        selection.reconcile();
      } else {
        notify("warning", "map_gap", "Map changed; refreshing layout");
        void hub?.requestMapSnapshot();
      }
    },
    onSnapshot: (snapshot) => {
      traffic.addPayload(snapshot);
      const result = store.applySnapshot(snapshot, performance.now());
      if (result.status === "applied") targeting.clearTarget();
      handleApply(result.status, result.tick, result.requestMapSnapshot === true);
    },
    onSnapshotDelta: (delta) => {
      traffic.addPayload(delta);
      const result = store.applyDelta(delta, performance.now());
      handleApply(result.status, result.tick, result.requestMapSnapshot === true);
      if (result.requestFullSnapshot) {
        notify("warning", "snapshot_gap", "Recovering the world state");
        void hub?.requestSnapshot(null);
      }
    },
    onSnapshotRejected: (rejection) => {
      notify(
        "warning",
        "snapshot_rejected",
        rejection.message ?? rejection.error ?? "Snapshot request was rejected"
      );
    },
    onIntentRejected: (rejection) => {
      traffic.addPayload(rejection);
      intent.handleRejected(rejection.sequence, rejection.code, rejection.message);
    },
    onPlayerLifecycle: (notice) => {
      if (notice.playerId !== ui.getState().playerId) return;

      if (notice.kind === "died") {
        store.setOwnGenos(0);
        centeredOnPlayer = false;
        ui.patch({ ownGenosNetworkId: 0 });
        if (notice.canRespawn) {
          respawnPopin.show({
            name: notice.name ?? "Your genos",
            age: notice.age,
            cause: notice.cause
          });
        } else {
          notify("warning", "awaiting_continuation", "Your genos is awaiting continuation.");
        }
        return;
      }

      store.setOwnGenos(notice.genosNetworkId);
      centeredOnPlayer = false;
      ui.patch({ ownGenosNetworkId: notice.genosNetworkId });
      respawnPopin.hide();
      if (notice.name !== null) {
        notify("success", "respawned", `You are now ${notice.name}.`);
      }
    },
    onWorldReset: (reset) => {
      traffic.addPayload(reset);
      store.reset();
      centeredOnPlayer = false;
      respawnPopin.hide();
      selection.select(null);
      targeting.clearAll();
      ui.patch({
        ownGenosNetworkId: 0,
        mapId: reset.mapId,
        mapVersion: reset.mapVersion,
        tick: reset.tick
      });
      notify("info", "world_reset", "The world was replaced; reloading map and world state.");
      void hub?.requestWelcome();
      void hub?.requestMapSnapshot();
      void hub?.requestSnapshot(null);
    },
    onStateChange: (state) => {
      ui.patch({ connection: state, reconnectCount: hub?.reconnects ?? 0 });
      if (state === "failed" && sceneReady) {
        notify("error", "connection_failed", "Connection lost. Use Retry to reconnect.");
      }
    },
    onReconnected: () => {
      void recover();
      void chatPanel.refresh();
      void relationshipsPanel.refresh();
      void venuePanel.refresh();
    },
    onChatMessage: (message) => {
      audio.play("chat");
      chatPanel.addMessage(message);
    },
    onEmote: (emote) => {
      audio.play("chat");
      chatPanel.showEmote(emote);
    }
  };

  function notify(kind: NotificationKind, code: string, message: string): void {
    notifications.push(kind, code, message);
    if (kind === "error") ui.patch({ lastError: message });
  }

  async function requestRespawn(): Promise<RespawnPopinResult> {
    if (useFixtures) {
      return { ok: false, message: "Respawn is not available in fixture mode." };
    }

    const token = session.store.token;
    if (token === null) {
      return { ok: false, message: "Sign in again to respawn." };
    }

    try {
      const response = await fetchImpl(`${config.apiBaseUrl}/api/player/respawn`, {
        method: "POST",
        headers: {
          Authorization: `Bearer ${token}`,
          "Content-Type": "application/json"
        },
        body: "{}"
      });
      const body = (await response.json().catch(() => null)) as
        | { code?: string; message?: string; genosNetworkId?: number; name?: string }
        | null;

      if (!response.ok || body === null || typeof body.genosNetworkId !== "number") {
        return { ok: false, message: body?.message ?? "Could not respawn." };
      }

      store.setOwnGenos(body.genosNetworkId);
      centeredOnPlayer = false;
      ui.patch({ ownGenosNetworkId: body.genosNetworkId });
      notify("success", "respawned", `You are now ${body.name ?? "your child"}.`);
      return { ok: true };
    } catch (error) {
      return {
        ok: false,
        message: error instanceof Error ? error.message : "Could not respawn."
      };
    }
  }

  function rejectionMessage(code: string, message: string): string {
    const messages: Record<string, string> = {
      blocked: "That tile is blocked",
      out_of_bounds: "That tile is outside the map",
      out_of_range: "Walk to the entrance first",
      role_not_allowed: "You cannot use this building",
      building_not_found: "That building no longer exists",
      genos_not_found: "Your genos is not available",
      not_owner: "You do not control that genos",
      stale_sequence: "That action was already handled"
    };
    return message !== "" ? message : (messages[code] ?? "Action not accepted");
  }

  function handleApply(
    status: "applied" | "stale" | "gap" | "map-stale",
    tick: number,
    requestMap: boolean
  ): void {
    if (status === "applied") {
      lastAppliedAt = Date.now();
      void hub?.acknowledgeSnapshot(tick);
      selection.reconcile();
      centerOnPlayerOnce();
      ui.patch({
        tick,
        totalGenoses: store.genoses.size,
        mapId: store.map?.mapId ?? "",
        mapVersion: store.map?.version ?? 0
      });
      if (!sceneReady) {
        sceneReady = true;
        loading.setStep("scene", "done");
        loading.hide();
      }
      return;
    }
    if (status === "map-stale" || requestMap) {
      void hub?.requestMapSnapshot();
    }
  }

  function centerOnPlayerOnce(): void {
    if (centeredOnPlayer || store.ownGenosId <= 0) return;
    const genos = store.genoses.get(store.ownGenosId);
    if (genos === undefined) return;
    camera.centerOnTile(genos.tileX, genos.tileY);
    camera.setZoom(GAME_CONSTANTS.pixelsPerTile);
    centeredOnPlayer = true;
  }

  function onWelcome(welcome: WelcomeMessage): void {
    try {
      validateWelcome(welcome);
    } catch (error) {
      notify("error", "welcome_invalid", error instanceof Error ? error.message : "Welcome rejected");
      return;
    }
    if (welcome.protocolVersion !== info.protocolVersion) {
      notify(
        "error",
        "protocol_mismatch",
        `Server protocol ${welcome.protocolVersion} is not supported by this client`
      );
      void hub?.stop();
      loading.fail(`Server protocol ${welcome.protocolVersion} is not supported by this client`);
      return;
    }
    store.setOwnGenos(welcome.genosNetworkId);
    renderer?.setTickInterval(welcome.tickIntervalMs);
    ui.patch({
      ownGenosNetworkId: welcome.genosNetworkId,
      mapId: welcome.mapId,
      mapVersion: welcome.mapVersion,
      tick: welcome.tick
    });

    if (welcome.awaitingRespawn) {
      centeredOnPlayer = false;
      respawnPopin.show({
        name: welcome.lastGenosName ?? "Your genos",
        age: welcome.lastGenosAge ?? 0
      });
    } else {
      respawnPopin.hide();
    }
  }

  function onViewportRect(rect: TileRect): void {
    if (disposed) return;
    if (!useFixtures && (hub === null || hub.currentState !== "connected")) return;
    viewportSync.update(visibleRectToViewport(rect));
  }

  function onSceneStats(stats: SceneStats): void {
    ui.patch({
      fps: stats.fps,
      frameTimeMs: stats.frameTimeMs,
      visibleGenoses: stats.visibleGenoses,
      totalGenoses: stats.totalGenoses,
      tick: stats.tick,
      bufferFrames: stats.bufferFrames,
      interpolationDelayMs: stats.interpolationDelayMs,
      cameraCenterX: stats.cameraX,
      cameraCenterY: stats.cameraY,
      cameraZoom: stats.zoom,
      renderer: stats.renderer,
      mapId: stats.mapId,
      mapVersion: stats.mapVersion,
      bytesPerSecond: traffic.bytesPerSecond(),
      reconnectCount: hub?.reconnects ?? 0,
      localPlayerVisible: stats.localPlayerVisible
    });
    if (settings.settings.debugOverlay) debugInfo.update(debugMetrics(stats));
  }

  function debugMetrics(stats?: SceneStats) {
    const state = ui.getState();
    return {
      fps: stats?.fps ?? state.fps,
      frameTimeMs: stats?.frameTimeMs ?? state.frameTimeMs,
      mapId: state.mapId,
      mapVersion: state.mapVersion,
      cameraX: state.cameraCenterX,
      cameraY: state.cameraCenterY,
      zoom: state.cameraZoom,
      visibleTiles: stats?.visibleTiles ?? 0,
      visibleBuildings: stats?.visibleBuildings ?? 0,
      visibleGenoses: state.visibleGenoses,
      totalGenoses: state.totalGenoses,
      tick: state.tick,
      bufferFrames: state.bufferFrames,
      interpolationDelayMs: state.interpolationDelayMs,
      inputAckP50Ms: state.inputAckP50Ms,
      inputAckP95Ms: state.inputAckP95Ms,
      bytesPerSecond: state.bytesPerSecond,
      reconnectCount: state.reconnectCount,
      renderer: state.renderer,
      buildStamp: state.buildStamp
    };
  }

  async function recover(): Promise<void> {
    const outcome = await recoverSession(
      {
        ensureAuth: async () => (await session.ensureValidToken()) !== null,
        // A reconnect cannot observe map versions that changed while offline.
        hasFreshMap: () => false,
        lastAppliedTick: () => store.tick,
        lastTickAgeMs: () => (lastAppliedAt === 0 ? Number.POSITIVE_INFINITY : Date.now() - lastAppliedAt),
        requestMapSnapshot: async () => {
          await hub?.requestMapSnapshot();
        },
        requestSnapshot: async (sinceTick) => {
          await hub?.requestSnapshot(sinceTick);
        }
      },
      {
        onError: (message) => notify("error", "recovery_failed", message)
      }
    );

    if (outcome === "recovered") {
      notify("info", "reconnected", "World state restored");
      viewportSync.reset();
      await viewportSync.resync();
    } else if (outcome === "auth-failed") {
      notify("error", "auth_expired", "Your session expired. Sign in again.");
    }
  }

  async function ensureAuthenticated(player: string): Promise<boolean> {
    loading.setStep("auth", "active");
    const restored = session.restore();
    if (restored.status === "authenticated" && restored.playerId === player) {
      loading.setStep("auth", "done");
      return true;
    }

    const state = await session.signIn(player);
    if (state.status === "authenticated") {
      loading.setStep("auth", "done");
      ui.patch({ playerId: state.playerId });
      return true;
    }

    loading.setStep("auth", "failed");
    const reason = state.status === "rejected" ? state.reason : "Authentication failed";
    ui.patch({ lastError: reason });
    loading.fail(reason);
    return false;
  }

  async function establish(player: string): Promise<void> {
    if (!(await ensureAuthenticated(player))) return;
    const token = session.store.token;
    if (token === null) return;

    if (hub !== null) {
      await hub.stop();
      hub = null;
    }

    const client = new WorldHubClient(config.hubUrl, handlers, { logger });
    hub = client;
    try {
      await client.connect(token);
      ui.patch({ connection: client.currentState, playerId: player });
      void chatPanel.refresh();
      void relationshipsPanel.refresh();
      void venuePanel.refresh();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Could not connect to the world";
      ui.patch({ lastError: message });
      notify("error", "connection_failed", message);
      loading.fail(message);
    }
  }

  async function startFixtures(player: string): Promise<void> {
    loading.setStep("auth", "done", "fixture mode");
    try {
      const base = import.meta.env.BASE_URL || "/";
      const [mapResponse, snapshotResponse] = await Promise.all([
        fetchImpl(`${base}src/test/fixtures/map-snapshot.json`),
        fetchImpl(`${base}src/test/fixtures/world-snapshot.json`)
      ]);
      if (!mapResponse.ok || !snapshotResponse.ok) {
        throw new Error("Fixture files were not found");
      }
      const map = validateMapSnapshot(await mapResponse.json());
      const snapshot = validateWorldSnapshot(await snapshotResponse.json());
      const own = snapshot.genoses.find((genos) => genos.isPlayerControlled);
      store.setOwnGenos(own?.networkId ?? 0);
      const decoded = store.applyMapSnapshot(map);
      camera.setMap(decoded);
      store.applySnapshot(snapshot, performance.now());
      ui.patch({
        connection: "connected",
        playerId: player,
        ownGenosNetworkId: own?.networkId ?? 0,
        mapId: map.mapId,
        mapVersion: map.version,
        tick: snapshot.tick,
        totalGenoses: snapshot.genoses.length
      });
      centeredOnPlayer = false;
      centerOnPlayerOnce();
      sceneReady = true;
      loading.setStep("scene", "done");
      loading.hide();
    } catch (error) {
      const message = error instanceof Error ? error.message : "Fixture mode failed";
      loading.setStep("scene", "failed");
      loading.fail(message);
      notify("error", "fixture_failed", message);
    }
  }

  const runtime: ClientRuntime = {
    connect: (player) => establish(player),
    signOut: async () => {
      if (hub !== null) {
        await hub.stop();
        hub = null;
      }
      session.signOut();
      store.reset();
      respawnPopin.hide();
      selection.select(null);
      targeting.clearAll();
      ui.reset();
      ui.patch({ connection: "closed", buildStamp: info.stamp, protocolVersion: info.protocolVersion });
      notify("info", "signed_out", "Signed out");
    },
    retry: async () => {
      notifications.clear();
      ui.patch({ lastError: "" });
      await establish(playerId);
    },
    dispose: () => {
      if (disposed) return;
      disposed = true;
      settingsUnsubscribe();
      unsubscribeAudioNotifications();
      window.removeEventListener("pointerdown", unlockAudio, true);
      window.removeEventListener("keydown", unlockAudio, true);
      audio.dispose();
      touchInput.dispose();
      cameraInput.dispose();
      targeting.dispose();
      respawnPopin.dispose();
      hud.dispose();
      playerDirectory.dispose();
      chatPanel.dispose();
      relationshipsPanel.dispose();
      venuePanel.dispose();
      renderer?.dispose();
      debugInfo.destroy();
      viewportSync.reset();
      void hub?.stop();
      pixi.destroy();
      hudRoot.remove();
      loading.destroy();
    }
  };

  loading.setRetry(() => void runtime.retry());

  if (useFixtures) {
    void startFixtures(playerId);
  } else {
    void establish(playerId);
  }

  return runtime;
}

function notificationCue(kind: NotificationKind, code: string): AudioCue {
  const normalized = code.toLowerCase();
  if (normalized.includes("outage")) return "utilityOutage";
  if (normalized.includes("birth") || normalized.includes("pregnan")) return "birth";
  if (normalized.includes("death") || normalized.includes("died") || normalized.includes("estate")) {
    return "death";
  }
  if (kind === "error") return "uiCancel";
  if (
    normalized === "intent_admitted" ||
    normalized === "building_requested" ||
    normalized === "reconnected" ||
    normalized === "signed_out"
  ) {
    return "uiConfirm";
  }
  return "notification";
}

function isFixtureRequested(): boolean {
  if (typeof window === "undefined") return false;
  try {
    return new URL(window.location.href).searchParams.get("fixtures") === "1";
  } catch {
    return false;
  }
}

function resolvePlayerId(config: ClientConfig): string {
  if (typeof window !== "undefined") {
    try {
      const requested = new URL(window.location.href).searchParams.get("playerId");
      if (requested !== null && requested.trim() !== "") return requested.trim();
      const stored = window.sessionStorage.getItem("genelife.playerId");
      if (stored !== null && stored.trim() !== "") return stored.trim();
      const generated = `${config.devPlayerId}-${Math.random().toString(36).slice(2, 8)}`;
      window.sessionStorage.setItem("genelife.playerId", generated);
      return generated;
    } catch {
      // Fall through to a generated id.
    }
  }
  return `${config.devPlayerId}-${Math.random().toString(36).slice(2, 8)}`;
}
