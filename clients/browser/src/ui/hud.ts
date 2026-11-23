import type { MapBuilding } from "../contracts/hub";
import { describeBuilding } from "./building-panel";
import type { ConnectionState } from "../net/events";
import type { Notification, NotificationCenter } from "./notifications";
import type {
  ClientSettings,
  KeyBindingAction,
  SettingsStore
} from "./settings";
import type { Selection, UiStore } from "./ui-store";

export interface HudActions {
  onInteract: () => void;
  onSignOut: () => void;
  onRetry: () => void;
  onSelectBuilding: (building: MapBuilding | null) => void;
  focusCanvas: () => void;
}

export interface HudOptions {
  root: HTMLElement;
  ui: UiStore;
  notifications: NotificationCenter;
  settings: SettingsStore;
  actions: HudActions;
  updateIntervalMs?: number;
}

const CONNECTION_LABEL: Record<ConnectionState, string> = {
  idle: "Idle",
  connecting: "Connecting…",
  connected: "Connected",
  reconnecting: "Reconnecting…",
  failed: "Connection failed",
  closed: "Disconnected"
};

interface Elements {
  connection: HTMLElement;
  player: HTMLElement;
  ownSim: HTMLElement;
  map: HTMLElement;
  tick: HTMLElement;
  version: HTMLElement;
  fps: HTMLElement;
  frame: HTMLElement;
  sims: HTMLElement;
  viewport: HTMLElement;
  buffer: HTMLElement;
  interp: HTMLElement;
  build: HTMLElement;
  renderer: HTMLElement;
  bytes: HTMLElement;
  lastError: HTMLElement;
  selection: HTMLElement;
  selectionName: HTMLElement;
  selectionMeta: HTMLElement;
  selectionOwner: HTMLElement;
  selectionHint: HTMLElement;
  interact: HTMLButtonElement;
  clearSelection: HTMLButtonElement;
  retry: HTMLButtonElement;
  signOut: HTMLButtonElement;
  notificationList: HTMLElement;
  ariaLive: HTMLElement;
  settingsPanel: HTMLElement;
  settingsToggle: HTMLButtonElement;
  reduceMotion: HTMLInputElement;
  debugOverlay: HTMLInputElement;
  hudScale: HTMLInputElement;
  bindingList: HTMLElement;
  connectionCard: HTMLElement;
  bottomRight: HTMLElement;
}

/**
 * DOM/CSS HUD. It owns no simulation state: it renders coalesced view models at
 * 10 Hz and uses `textContent` for every server-provided string.
 */
export class Hud {
  private readonly elements: Elements;
  private readonly timer: ReturnType<typeof setInterval>;
  private readonly unsubscribeUi: () => void;
  private readonly unsubscribeNotifications: () => void;
  private readonly unsubscribeSettings: () => void;
  private dirty = true;
  private lastNotifications: readonly Notification[] = [];
  private lastSettings: ClientSettings | null = null;
  private rebindAction: KeyBindingAction | null = null;
  private readonly options: HudOptions;

  private readonly onDocumentKeyDown = (event: KeyboardEvent): void => {
    if (event.key === "Escape") this.options.actions.focusCanvas();
  };

  constructor(options: HudOptions) {
    this.options = options;
    this.elements = buildDom(options);
    this.unsubscribeUi = options.ui.subscribe(() => {
      this.dirty = true;
    });
    this.unsubscribeNotifications = options.notifications.subscribe((items) => {
      this.lastNotifications = items;
      this.renderNotifications();
      this.dirty = true;
    });
    this.unsubscribeSettings = options.settings.subscribe((settings) => {
      this.lastSettings = settings;
      this.renderSettings(settings);
      this.dirty = true;
    });

    this.timer = setInterval(() => this.flush(), options.updateIntervalMs ?? 100);
    document.addEventListener("keydown", this.onDocumentKeyDown);
    this.flush();
  }

  dispose(): void {
    clearInterval(this.timer);
    this.unsubscribeUi();
    this.unsubscribeNotifications();
    this.unsubscribeSettings();
    document.removeEventListener("keydown", this.onDocumentKeyDown);
  }

  private flush(): void {
    if (!this.dirty) return;
    this.dirty = false;
    const state = this.options.ui.getState();
    const e = this.elements;

    setText(e.connection, CONNECTION_LABEL[state.connection]);
    e.connectionCard.dataset.connection = state.connection;
    setText(e.player, state.playerId || "not signed in");
    setText(e.ownSim, state.ownSimNetworkId > 0 ? String(state.ownSimNetworkId) : "—");
    setText(e.map, state.mapId || "—");
    setText(e.version, String(state.mapVersion));
    setText(e.tick, String(state.tick));
    setText(e.fps, state.fps.toFixed(0));
    setText(e.frame, `${state.frameTimeMs.toFixed(1)} ms`);
    setText(e.sims, `${state.visibleSims}/${state.totalSims}`);
    setText(e.viewport, String(state.viewportRadius));
    setText(e.buffer, String(state.bufferFrames));
    setText(e.interp, `${state.interpolationDelayMs} ms`);
    setText(e.build, state.buildStamp);
    setText(e.renderer, state.renderer || "—");
    setText(e.bytes, `${(state.bytesPerSecond / 1024).toFixed(1)} KiB/s`);
    setText(e.lastError, state.lastError || "none");
    e.retry.hidden = state.connection !== "failed" && state.connection !== "closed";
    this.renderSelection(state.selection);
    e.bottomRight.hidden = state.selection === null && e.retry.hidden;
  }

  private renderSelection(selection: Selection): void {
    const e = this.elements;
    if (selection === null) {
      e.selection.hidden = true;
      return;
    }

    e.selection.hidden = false;
    if (selection.kind === "building") {
      const details = describeBuilding(selection.building);
      setText(e.selectionName, details.name);
      setText(e.selectionMeta, details.meta);
      setText(e.selectionOwner, details.owner);
      setText(e.selectionHint, details.hint);
      e.interact.hidden = false;
    } else {
      setText(e.selectionName, `Tile (${selection.x}, ${selection.y})`);
      setText(e.selectionMeta, "Target tile");
      setText(e.selectionOwner, "");
      setText(e.selectionHint, "");
      e.interact.hidden = true;
    }
  }

  private renderNotifications(): void {
    const list = this.elements.notificationList;
    list.replaceChildren();
    for (const notification of this.lastNotifications) {
      const item = document.createElement("li");
      item.className = `notification notification-${notification.kind}`;
      item.dataset.code = notification.code;
      const text = document.createElement("span");
      text.textContent = notification.message;
      const dismiss = document.createElement("button");
      dismiss.type = "button";
      dismiss.textContent = "Dismiss";
      dismiss.setAttribute("aria-label", "Dismiss notification");
      dismiss.addEventListener("click", () => this.options.notifications.dismiss(notification.id));
      item.append(text, dismiss);
      list.appendChild(item);
    }

    const assertive = this.lastNotifications.filter((item) => item.kind === "error");
    this.elements.ariaLive.textContent =
      assertive.length > 0 ? (assertive[assertive.length - 1]?.message ?? "") : "";
  }

  private renderSettings(settings: ClientSettings): void {
    const e = this.elements;
    if (this.lastSettings !== null) {
      e.reduceMotion.checked = settings.reduceMotion;
      e.debugOverlay.checked = settings.debugOverlay;
      e.hudScale.value = String(settings.hudScale);
    }
    e.bindingList.replaceChildren();
    for (const action of Object.keys(settings.bindings) as KeyBindingAction[]) {
      const row = document.createElement("li");
      const label = document.createElement("span");
      label.textContent = action;
      const keys = document.createElement("code");
      keys.textContent = settings.bindings[action].join(" / ");
      const rebind = document.createElement("button");
      rebind.type = "button";
      rebind.textContent = this.rebindAction === action ? "Press a key…" : "Rebind";
      rebind.addEventListener("click", () => {
        this.rebindAction = action;
        this.dirty = true;
        const capture = (event: KeyboardEvent): void => {
          if (isTypingTarget(event.target)) return;
          event.preventDefault();
          event.stopPropagation();
          window.removeEventListener("keydown", capture, true);
          this.rebindAction = null;
          if (event.key !== "Escape") {
            this.options.settings.setBinding(action, [event.key.toLowerCase()]);
          }
          this.dirty = true;
        };
        window.addEventListener("keydown", capture, true);
      });
      row.append(label, keys, rebind);
      e.bindingList.appendChild(row);
    }
  }
}

function buildDom(options: HudOptions): Elements {
  const root = options.root;
  root.replaceChildren();
  root.id = "hud";

  const topLeft = panel("hud-panel hud-top-left");
  const connectionCard = div("connection-card");
  const connection = div("value");
  const player = div("value");
  const ownSim = div("value");
  connectionCard.append(row("Connection", connection), row("Player", player), row("Sim", ownSim));
  const signOut = button("Sign out");
  signOut.addEventListener("click", () => options.actions.onSignOut());
  topLeft.append(connectionCard, signOut);
  addControl(signOut);

  const topRight = panel("hud-panel hud-top-right");
  const map = div("value");
  const version = div("value");
  const tick = div("value");
  const fps = div("value");
  const frame = div("value");
  const sims = div("value");
  const viewport = div("value");
  const buffer = div("value");
  const interp = div("value");
  const build = div("value");
  const renderer = div("value");
  const bytes = div("value");
  const lastError = div("value hud-error");
  topRight.append(
    row("Map", map),
    row("Version", version),
    row("Tick", tick),
    row("FPS", fps),
    row("Frame", frame),
    row("Sims", sims),
    row("Viewport r", viewport),
    row("Buffer", buffer),
    row("Interp", interp),
    row("Renderer", renderer),
    row("Rate", bytes),
    row("Build", build),
    row("Last error", lastError)
  );

  const bottomLeft = panel("hud-panel hud-bottom-left");
  const notificationList = document.createElement("ul");
  notificationList.className = "notification-list";
  notificationList.setAttribute("aria-label", "Notifications");
  const ariaLive = div("sr-only");
  ariaLive.setAttribute("role", "status");
  ariaLive.setAttribute("aria-live", "assertive");
  bottomLeft.append(notificationList, ariaLive);

  const bottomRight = panel("hud-panel hud-bottom-right");
  const selection = div("selection");
  const selectionName = div("selection-name");
  const selectionMeta = div("selection-meta");
  const selectionOwner = div("selection-owner");
  const selectionHint = div("selection-hint");
  const interact = button("Interact");
  interact.addEventListener("click", () => options.actions.onInteract());
  addControl(interact);
  const clearSelection = button("Clear selection");
  clearSelection.addEventListener("click", () => options.actions.onSelectBuilding(null));
  addControl(clearSelection);
  selection.append(selectionName, selectionMeta, selectionOwner, selectionHint, interact, clearSelection);
  selection.hidden = true;
  const retry = button("Retry connection");
  retry.hidden = true;
  retry.addEventListener("click", () => options.actions.onRetry());
  addControl(retry);
  bottomRight.append(selection, retry);

  const settingsToggle = button("Settings");
  settingsToggle.setAttribute("aria-expanded", "false");
  settingsToggle.classList.add("settings-toggle");
  addControl(settingsToggle);
  const settingsPanel = panel("hud-panel hud-settings");
  settingsPanel.hidden = true;
  const reduce = checkbox("Reduce motion");
  reduce.input.addEventListener("change", () =>
    options.settings.update({ reduceMotion: reduce.input.checked })
  );
  const debug = checkbox("Debug overlay");
  debug.input.addEventListener("change", () =>
    options.settings.update({ debugOverlay: debug.input.checked })
  );
  const hudScale = document.createElement("input");
  hudScale.type = "range";
  hudScale.min = "0.8";
  hudScale.max = "1.6";
  hudScale.step = "0.1";
  hudScale.setAttribute("aria-label", "HUD scale");
  hudScale.addEventListener("input", () =>
    options.settings.update({ hudScale: Number(hudScale.value) })
  );
  const bindingList = document.createElement("ul");
  bindingList.className = "binding-list";
  const resetSettings = button("Reset settings");
  resetSettings.addEventListener("click", () => options.settings.reset());
  addControl(resetSettings);
  settingsPanel.append(
    reduce.wrapper,
    debug.wrapper,
    labelled("HUD scale", hudScale),
    labelled("Key bindings", bindingList),
    resetSettings
  );
  settingsToggle.addEventListener("click", () => {
    settingsPanel.hidden = !settingsPanel.hidden;
    settingsToggle.setAttribute("aria-expanded", String(!settingsPanel.hidden));
  });

  root.append(topLeft, topRight, bottomLeft, bottomRight, settingsToggle, settingsPanel);

  return {
    connection,
    player,
    ownSim,
    map,
    version,
    tick,
    fps,
    frame,
    sims,
    viewport,
    buffer,
    interp,
    build,
    renderer,
    bytes,
    lastError,
    selection,
    selectionName,
    selectionMeta,
    selectionOwner,
    selectionHint,
    interact,
    clearSelection,
    retry,
    signOut,
    notificationList,
    ariaLive,
    settingsPanel,
    settingsToggle,
    reduceMotion: reduce.input,
    debugOverlay: debug.input,
    hudScale,
    bindingList,
    connectionCard,
    bottomRight
  };
}

function panel(className: string): HTMLElement {
  const element = document.createElement("section");
  element.className = className;
  element.dataset.hudControl = "true";
  return element;
}

function div(className: string): HTMLElement {
  const element = document.createElement("div");
  if (className !== "") element.className = className;
  return element;
}

function row(label: string, value: HTMLElement): HTMLElement {
  const element = document.createElement("div");
  element.className = "hud-row";
  const key = document.createElement("span");
  key.className = "hud-key";
  key.textContent = label;
  value.dataset.field = label.toLowerCase().replace(/\s+/g, "-");
  element.append(key, value);
  return element;
}

function button(label: string): HTMLButtonElement {
  const element = document.createElement("button");
  element.type = "button";
  element.textContent = label;
  return element;
}

function checkbox(label: string): { wrapper: HTMLLabelElement; input: HTMLInputElement } {
  const input = document.createElement("input");
  input.type = "checkbox";
  const wrapper = document.createElement("label");
  wrapper.className = "hud-checkbox";
  wrapper.append(input, document.createTextNode(label));
  return { wrapper, input };
}

function labelled(label: string, control: HTMLElement): HTMLElement {
  const wrapper = document.createElement("label");
  wrapper.className = "hud-labelled";
  const text = document.createElement("span");
  text.textContent = label;
  wrapper.append(text, control);
  return wrapper;
}

function addControl(element: HTMLElement): void {
  element.dataset.hudControl = "true";
}

function setText(element: HTMLElement, value: string): void {
  if (element.textContent !== value) element.textContent = value;
}

function isTypingTarget(target: EventTarget | null): boolean {
  return target instanceof HTMLInputElement;
}
