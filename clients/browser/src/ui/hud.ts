import type { MapBuilding } from "../contracts/hub";
import { describeBuilding, type BuildingOccupants, type WorldPopulation } from "./building-panel";
import { describeGenos } from "./genos-panel";
import { HudDock } from "./hud-dock";
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
  /** Clears the inspected genos (network id) — used by Escape and the Close button. */
  onSelectGenos: (networkId: number | null) => void;
  focusCanvas: () => void;
}

export interface HudOptions {
  root: HTMLElement;
  ui: UiStore;
  notifications: NotificationCenter;
  settings: SettingsStore;
  actions: HudActions;
  dock?: HudDock;
  updateIntervalMs?: number;
  /**
   * Live world data used to resolve building owners and occupants. Read on each
   * flush, so the lists follow snapshots without holding a second copy here.
   */
  world?: () => WorldPopulation;
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
  dock: HudDock;
  connection: HTMLElement;
  player: HTMLElement;
  ownGenos: HTMLElement;
  map: HTMLElement;
  tick: HTMLElement;
  version: HTMLElement;
  fps: HTMLElement;
  frame: HTMLElement;
  genoses: HTMLElement;
  viewport: HTMLElement;
  buffer: HTMLElement;
  interp: HTMLElement;
  inputAck: HTMLElement;
  build: HTMLElement;
  renderer: HTMLElement;
  bytes: HTMLElement;
  lastError: HTMLElement;
  selection: HTMLElement;
  selectionName: HTMLElement;
  selectionMeta: HTMLElement;
  selectionOwner: HTMLElement;
  selectionOccupants: HTMLElement;
  selectionHint: HTMLElement;
  interact: HTMLButtonElement;
  clearSelection: HTMLButtonElement;
  retry: HTMLButtonElement;
  signOut: HTMLButtonElement;
  notificationList: HTMLElement;
  ariaLive: HTMLElement;
  settingsPanel: HTMLElement;
  reduceMotion: HTMLInputElement;
  debugOverlay: HTMLInputElement;
  chatVisible: HTMLInputElement;
  hudScale: HTMLInputElement;
  soundVolume: HTMLInputElement;
  audioMuted: HTMLInputElement;
  audioCategoryList: HTMLElement;
  touchControls: HTMLInputElement;
  handedness: HTMLSelectElement;
  bindingList: HTMLElement;
  connectionCard: HTMLElement;
  notificationsPanel: HTMLElement;
  bottomRight: HTMLElement;
  genosPanel: HTMLElement;
  genosName: HTMLElement;
  genosStatus: HTMLElement;
  genosMeta: HTMLElement;
  genosAction: HTMLElement;
  genosNeeds: HTMLElement;
  genosMoney: HTMLElement;
  genosHousing: HTMLElement;
  genosHousehold: HTMLElement;
  genosHousingUnit: HTMLElement;
  genosHint: HTMLElement;
  genosClose: HTMLButtonElement;
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
    if (event.key !== "Escape") return;
    if (this.elements.dock.isOpen) {
      this.elements.dock.close(true);
      return;
    }
    const selection = this.options.ui.getState().selection;
    if (selection !== null && selection.kind === "genos") {
      this.options.actions.onSelectGenos(null);
      return;
    }
    this.options.actions.focusCanvas();
  };

  private readonly onDocumentPointerDown = (event: PointerEvent): void => {
    if (!this.elements.dock.isOpen) return;
    if (event.target instanceof Node && this.elements.dock.contains(event.target)) return;
    this.elements.dock.close();
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
    document.addEventListener("pointerdown", this.onDocumentPointerDown, true);
    this.flush();
  }

  dispose(): void {
    clearInterval(this.timer);
    this.unsubscribeUi();
    this.unsubscribeNotifications();
    this.unsubscribeSettings();
    document.removeEventListener("keydown", this.onDocumentKeyDown);
    document.removeEventListener("pointerdown", this.onDocumentPointerDown, true);
  }

  private flush(): void {
    if (!this.dirty) return;
    this.dirty = false;
    const state = this.options.ui.getState();
    const e = this.elements;

    setText(e.connection, CONNECTION_LABEL[state.connection]);
    e.connectionCard.dataset.connection = state.connection;
    e.dock.setConnection(state.connection);
    setText(e.player, state.playerId || "not signed in");
    setText(e.ownGenos, state.ownGenosNetworkId > 0 ? String(state.ownGenosNetworkId) : "—");
    setText(e.map, state.mapId || "—");
    setText(e.version, String(state.mapVersion));
    setText(e.tick, String(state.tick));
    setText(e.fps, state.fps.toFixed(0));
    setText(e.frame, `${state.frameTimeMs.toFixed(1)} ms`);
    setText(e.genoses, `${state.visibleGenoses}/${state.totalGenoses}`);
    setText(e.viewport, String(state.viewportRadius));
    setText(e.buffer, String(state.bufferFrames));
    setText(e.interp, `${state.interpolationDelayMs} ms`);
    setText(e.inputAck, `p50 ${state.inputAckP50Ms.toFixed(0)} / p95 ${state.inputAckP95Ms.toFixed(0)} ms`);
    setText(e.build, state.buildStamp);
    setText(e.renderer, state.renderer || "—");
    setText(e.bytes, `${(state.bytesPerSecond / 1024).toFixed(1)} KiB/s`);
    setText(e.lastError, state.lastError || "none");
    e.retry.hidden = state.connection !== "failed" && state.connection !== "closed";
    this.renderSelection(state.selection);
    const hasRightPanel = state.selection !== null && state.selection.kind !== "genos";
    e.bottomRight.hidden = !hasRightPanel && e.retry.hidden;
  }

  private renderSelection(selection: Selection): void {
    const e = this.elements;
    this.renderGenosPanel(selection);

    if (selection === null) {
      e.selection.hidden = true;
      this.renderOccupants(null);
      return;
    }

    if (selection.kind === "building") {
      e.selection.hidden = false;
      const details = describeBuilding(selection.building, this.worldPopulation());
      setText(e.selectionName, details.name);
      setText(e.selectionMeta, details.meta);
      setText(e.selectionOwner, details.owner);
      setText(e.selectionHint, details.hint);
      this.renderOccupants(details.occupants);
      e.interact.hidden = false;
    } else if (selection.kind === "tile") {
      e.selection.hidden = false;
      setText(e.selectionName, `Tile (${selection.x}, ${selection.y})`);
      setText(e.selectionMeta, "Target tile");
      setText(e.selectionOwner, "");
      setText(e.selectionHint, "");
      this.renderOccupants(null);
      e.interact.hidden = true;
    } else {
      // A genos owns the left-side inspection panel, not this one.
      e.selection.hidden = true;
    }
  }

  private worldPopulation(): WorldPopulation {
    return this.options.world?.() ?? { genoses: [], companies: [] };
  }

  /**
   * Renders the residents/workers block for the selected building. Rows are
   * built from scratch on every flush with `textContent` only, so no server
   * string is ever parsed as HTML.
   */
  private renderOccupants(occupants: BuildingOccupants | null): void {
    const container = this.elements.selectionOccupants;
    container.replaceChildren();
    container.hidden = occupants === null;
    if (occupants === null) {
      delete container.dataset.occupants;
      return;
    }
    container.dataset.occupants = occupants.kind;

    const title = div("occupants-title");
    title.textContent = occupants.title;
    container.append(title);

    if (occupants.empty !== null) {
      const empty = div("occupants-empty");
      empty.textContent = occupants.empty;
      container.append(empty);
      return;
    }

    for (const group of occupants.groups) {
      const groupElement = div("occupant-group");
      if (group.label !== "") {
        const label = div("occupant-group-label");
        label.textContent = group.label;
        groupElement.append(label);
      }
      const list = document.createElement("ul");
      list.className = "occupant-list";
      for (const row of group.rows) {
        const item = document.createElement("li");
        item.className = "occupant";
        item.dataset.networkId = String(row.networkId);
        item.dataset.present = row.present ? "true" : "false";
        const name = document.createElement("span");
        name.className = "occupant-name";
        name.textContent = row.name;
        item.append(name);
        if (row.detail !== "") {
          const detail = document.createElement("span");
          detail.className = "occupant-detail";
          detail.textContent = row.detail;
          item.append(detail);
        }
        const presence = document.createElement("span");
        presence.className = "occupant-presence";
        presence.textContent = row.present ? "present" : "away";
        item.append(presence);
        list.append(item);
      }
      groupElement.append(list);
      if (group.hiddenCount > 0) {
        const more = div("occupant-more");
        more.textContent = `+${group.hiddenCount} more`;
        groupElement.append(more);
      }
      container.append(groupElement);
    }
  }

  private renderGenosPanel(selection: Selection): void {
    const e = this.elements;
    if (selection === null || selection.kind !== "genos") {
      e.genosPanel.hidden = true;
      return;
    }
    const details = describeGenos(selection.genos);
    e.genosPanel.hidden = false;
    setText(e.genosName, details.name);
    setText(e.genosStatus, details.status);
    e.genosStatus.dataset.dead = selection.genos.isDead ? "true" : "false";
    setText(e.genosMeta, details.meta);
    setText(e.genosAction, details.action);
    setText(e.genosNeeds, details.needs);
    setText(e.genosMoney, details.money);
    setText(e.genosHousing, details.housing);
    setText(e.genosHousehold, details.household);
    setText(e.genosHousingUnit, details.housingUnit);
    setText(e.genosHint, details.hint);
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
    this.elements.notificationsPanel.hidden = this.lastNotifications.length === 0;
  }

  private renderSettings(settings: ClientSettings): void {
    const e = this.elements;
    if (this.lastSettings !== null) {
      e.reduceMotion.checked = settings.reduceMotion;
      e.debugOverlay.checked = settings.debugOverlay;
      e.chatVisible.checked = settings.chatVisible;
      e.hudScale.value = String(settings.hudScale);
      e.soundVolume.value = String(settings.soundVolume);
      e.audioMuted.checked = settings.audioMuted;
      e.touchControls.checked = settings.touchControls;
      e.handedness.value = settings.handedness;
    }
    e.audioCategoryList.replaceChildren();
    for (const category of ["ui", "notification", "chat", "life", "ambient"] as const) {
      const wrapper = checkbox(category);
      wrapper.input.checked = settings.audioCategories[category];
      wrapper.input.addEventListener("change", () =>
        this.options.settings.setAudioCategory(category, wrapper.input.checked)
      );
      const row = document.createElement("li");
      row.appendChild(wrapper.wrapper);
      e.audioCategoryList.appendChild(row);
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

  const dock = options.dock ?? new HudDock(root);
  if (options.dock) root.appendChild(dock.element);

  const connectionCard = div("connection-card");
  const connection = div("value");
  const player = div("value");
  const ownGenos = div("value");
  connectionCard.append(row("Connection", connection), row("Player", player), row("Genos", ownGenos));

  const map = div("value");
  const version = div("value");
  const tick = div("value");
  const fps = div("value");
  const frame = div("value");
  const genoses = div("value");
  const viewport = div("value");
  const buffer = div("value");
  const interp = div("value");
  const inputAck = div("value");
  const build = div("value");
  const renderer = div("value");
  const bytes = div("value");
  const lastError = div("value hud-error");

  const statusPanel = panel("hud-status");
  statusPanel.append(
    section("Session", connectionCard),
    section(
      "World",
      row("Map", map),
      row("Version", version),
      row("Tick", tick),
      row("Genoses", genoses),
      row("Viewport r", viewport),
      row("Renderer", renderer)
    ),
    section(
      "Performance",
      row("FPS", fps),
      row("Frame", frame),
      row("Buffer", buffer),
      row("Interp", interp),
      row("Input ack", inputAck),
      row("Rate", bytes)
    ),
    section("Build", row("Build", build), row("Last error", lastError))
  );
  dock.addPanel({
    id: "status",
    label: "Status",
    icon: "status",
    order: 0,
    content: statusPanel
  });

  const notificationsPanel = panel("hud-panel hud-bottom-left");
  notificationsPanel.hidden = true;
  const notificationList = document.createElement("ul");
  notificationList.className = "notification-list";
  notificationList.setAttribute("aria-label", "Notifications");
  notificationsPanel.append(notificationList);
  const ariaLive = div("sr-only");
  ariaLive.setAttribute("role", "status");
  ariaLive.setAttribute("aria-live", "assertive");

  const bottomRight = panel("hud-panel hud-bottom-right");
  const selection = div("selection");
  const selectionName = div("selection-name");
  const selectionMeta = div("selection-meta");
  const selectionOwner = div("selection-owner");
  const selectionOccupants = div("selection-occupants");
  selectionOccupants.hidden = true;
  selectionOccupants.setAttribute("aria-label", "Occupants");
  const selectionHint = div("selection-hint");
  const interact = button("Interact");
  interact.addEventListener("click", () => options.actions.onInteract());
  addControl(interact);
  const clearSelection = button("Clear selection");
  clearSelection.addEventListener("click", () => options.actions.onSelectBuilding(null));
  addControl(clearSelection);
  selection.append(
    selectionName,
    selectionMeta,
    selectionOwner,
    selectionOccupants,
    selectionHint,
    interact,
    clearSelection
  );
  selection.hidden = true;
  const retry = button("Retry connection");
  retry.hidden = true;
  retry.addEventListener("click", () => options.actions.onRetry());
  addControl(retry);
  bottomRight.append(selection, retry);

  // Genos inspection panel: left column, stacked above the chat window.
  const genosPanel = panel("hud-panel hud-genos");
  genosPanel.hidden = true;
  genosPanel.setAttribute("aria-label", "Genos");
  const genosName = div("genos-name");
  const genosStatus = div("genos-status");
  const genosHeader = div("genos-header");
  const genosClose = button("Close");
  genosClose.setAttribute("aria-label", "Close genos panel");
  genosClose.addEventListener("click", () => options.actions.onSelectGenos(null));
  addControl(genosClose);
  genosHeader.append(genosName, genosClose);
  const genosMeta = div("value");
  const genosAction = div("value");
  const genosNeeds = div("value");
  const genosMoney = div("value");
  const genosHousing = div("value");
  const genosHousehold = div("value");
  const genosHousingUnit = div("value");
  const genosHint = div("genos-hint");
  genosPanel.append(
    genosHeader,
    genosStatus,
    row("Age", genosMeta),
    row("Action", genosAction),
    row("Needs", genosNeeds),
    row("Money", genosMoney),
    row("Housing", genosHousing),
    row("Household", genosHousehold),
    row("Housing unit", genosHousingUnit),
    genosHint
  );

  const settingsPanel = panel("hud-settings");
  settingsPanel.hidden = true;
  const reduce = checkbox("Reduce motion");
  reduce.input.addEventListener("change", () =>
    options.settings.update({ reduceMotion: reduce.input.checked })
  );
  const debug = checkbox("Debug overlay");
  debug.input.addEventListener("change", () =>
    options.settings.update({ debugOverlay: debug.input.checked })
  );
  const chatVisible = checkbox("Chat window");
  chatVisible.input.addEventListener("change", () =>
    options.settings.update({ chatVisible: chatVisible.input.checked })
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
  const soundVolume = document.createElement("input");
  soundVolume.type = "range";
  soundVolume.min = "0";
  soundVolume.max = "1";
  soundVolume.step = "0.05";
  soundVolume.setAttribute("aria-label", "Sound volume");
  soundVolume.addEventListener("input", () =>
    options.settings.update({ soundVolume: Number(soundVolume.value) })
  );
  const audioMuted = checkbox("Mute audio");
  audioMuted.input.addEventListener("change", () =>
    options.settings.update({ audioMuted: audioMuted.input.checked })
  );
  const audioCategoryList = document.createElement("ul");
  audioCategoryList.className = "audio-category-list";
  const touchControls = checkbox("Touch controls");
  touchControls.input.addEventListener("change", () =>
    options.settings.update({ touchControls: touchControls.input.checked })
  );
  const handedness = document.createElement("select");
  handedness.setAttribute("aria-label", "Touch handedness");
  for (const value of ["right", "left"] as const) {
    const option = document.createElement("option");
    option.value = value;
    option.textContent = value === "right" ? "Right-handed" : "Left-handed";
    handedness.appendChild(option);
  }
  handedness.addEventListener("change", () =>
    options.settings.update({ handedness: handedness.value === "left" ? "left" : "right" })
  );
  const bindingList = document.createElement("ul");
  bindingList.className = "binding-list";
  const resetSettings = button("Reset settings");
  resetSettings.addEventListener("click", () => options.settings.reset());
  addControl(resetSettings);
  settingsPanel.append(
    reduce.wrapper,
    debug.wrapper,
    chatVisible.wrapper,
    labelled("HUD scale", hudScale),
    labelled("Sound volume", soundVolume),
    audioMuted.wrapper,
    labelled("Audio categories", audioCategoryList),
    touchControls.wrapper,
    labelled("Handedness", handedness),
    labelled("Key bindings", bindingList),
    resetSettings
  );
  dock.addPanel({
    id: "settings",
    label: "Settings",
    icon: "settings",
    order: 90,
    content: settingsPanel
  });
  const signOut = dock.addAction({
    label: "Sign out",
    icon: "signout",
    onClick: () => {
      dock.close();
      options.actions.onSignOut();
    }
  });

  root.append(notificationsPanel, bottomRight, genosPanel, ariaLive);

  return {
    dock,
    connection,
    player,
    ownGenos,
    map,
    version,
    tick,
    fps,
    frame,
    genoses,
    viewport,
    buffer,
    interp,
    inputAck,
    build,
    renderer,
    bytes,
    lastError,
    selection,
    selectionName,
    selectionMeta,
    selectionOwner,
    selectionOccupants,
    selectionHint,
    interact,
    clearSelection,
    retry,
    signOut,
    notificationList,
    ariaLive,
    settingsPanel,
    reduceMotion: reduce.input,
    debugOverlay: debug.input,
    chatVisible: chatVisible.input,
    hudScale,
    soundVolume,
    audioMuted: audioMuted.input,
    audioCategoryList,
    touchControls: touchControls.input,
    handedness,
    bindingList,
    connectionCard,
    notificationsPanel,
    bottomRight,
    genosPanel,
    genosName,
    genosStatus,
    genosMeta,
    genosAction,
    genosNeeds,
    genosMoney,
    genosHousing,
    genosHousehold,
    genosHousingUnit,
    genosHint,
    genosClose
  };
}

function panel(className: string): HTMLElement {
  const element = document.createElement("section");
  element.className = className;
  element.dataset.hudControl = "true";
  return element;
}

function section(title: string, ...children: HTMLElement[]): HTMLElement {
  const element = document.createElement("div");
  element.className = "hud-section";
  const heading = document.createElement("div");
  heading.className = "hud-section-title";
  heading.textContent = title;
  element.append(heading, ...children);
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
