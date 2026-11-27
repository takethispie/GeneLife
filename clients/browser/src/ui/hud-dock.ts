import type { ConnectionState } from "../net/events";

export type DockIcon = "status" | "settings" | "users" | "chat" | "heart" | "pin" | "signout";

export interface DockPanelOptions {
  id: string;
  label: string;
  icon: DockIcon;
  content: HTMLElement;
  order?: number;
  onOpen?: () => void;
  onClose?: () => void;
}

export interface DockActionOptions {
  label: string;
  icon: DockIcon;
  onClick: () => void;
}

/**
 * A dock button that toggles something outside the dock panel (for example the
 * bottom-left chat window) instead of opening a panel. It keeps `aria-pressed`
 * in sync so assistive tech reports the window state.
 */
export interface DockToggleOptions {
  id: string;
  label: string;
  icon: DockIcon;
  order?: number;
  onToggle?: (pressed: boolean) => void;
}

export interface DockToggle {
  readonly button: HTMLButtonElement;
  setPressed(pressed: boolean): void;
}

interface DockEntry {
  options: DockPanelOptions;
  button: HTMLButtonElement;
}

const SVG_NS = "http://www.w3.org/2000/svg";

const ICONS: Record<DockIcon, readonly string[]> = {
  status: ["M22 12h-4l-3 9L9 3l-3 9H2"],
  settings: [
    "M4 21v-7",
    "M4 10V3",
    "M12 21v-9",
    "M12 8V3",
    "M20 21v-5",
    "M20 12V3",
    "M1 14h6",
    "M9 8h6",
    "M17 16h6"
  ],
  users: [
    "M17 21v-2a4 4 0 0 0-4-4H5a4 4 0 0 0-4 4v2",
    "M9 11a4 4 0 1 0 0-8 4 4 0 0 0 0 8",
    "M23 21v-2a4 4 0 0 0-3-3.87",
    "M16 3.13a4 4 0 0 1 0 7.75"
  ],
  chat: ["M21 15a2 2 0 0 1-2 2H7l-4 4V5a2 2 0 0 1 2-2h14a2 2 0 0 1 2 2z"],
  heart: [
    "M20.84 4.61a5.5 5.5 0 0 0-7.78 0L12 5.67l-1.06-1.06a5.5 5.5 0 0 0-7.78 7.78l1.06 1.06L12 21.23l7.78-7.78 1.06-1.06a5.5 5.5 0 0 0 0-7.78z"
  ],
  pin: [
    "M21 10c0 7-9 13-9 13s-9-6-9-13a9 9 0 0 1 18 0z",
    "M12 13a3 3 0 1 0 0-6 3 3 0 0 0 0 6z"
  ],
  signout: ["M9 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h4", "M16 17l5-5-5-5", "M21 12H9"]
};

const CONNECTION_TITLE: Record<ConnectionState, string> = {
  idle: "Idle",
  connecting: "Connecting",
  connected: "Connected",
  reconnecting: "Reconnecting",
  failed: "Connection failed",
  closed: "Disconnected"
};

export class HudDock {
  readonly element: HTMLElement;

  private readonly bar: HTMLElement;
  private readonly entriesHost: HTMLElement;
  private readonly actionsHost: HTMLElement;
  private readonly panel: HTMLElement;
  private readonly panelTitle: HTMLElement;
  private readonly panelBody: HTMLElement;
  private readonly entries = new Map<string, DockEntry>();
  private readonly toggles = new Map<string, DockToggle>();
  private activeId: string | null = null;

  constructor(root: HTMLElement) {
    this.element = document.createElement("div");
    this.element.className = "hud-dock";

    this.bar = document.createElement("div");
    this.bar.className = "hud-dock-bar";
    this.bar.setAttribute("role", "toolbar");
    this.bar.setAttribute("aria-label", "World controls");

    this.entriesHost = document.createElement("div");
    this.entriesHost.className = "hud-dock-entries";
    this.actionsHost = document.createElement("div");
    this.actionsHost.className = "hud-dock-actions";
    this.bar.append(this.entriesHost, this.actionsHost);
    this.setPointerControls(this.bar);

    this.panel = document.createElement("section");
    this.panel.className = "hud-panel hud-dock-panel";
    this.panel.hidden = true;
    this.setPointerControls(this.panel);

    const header = document.createElement("div");
    header.className = "hud-dock-panel-header";
    this.panelTitle = document.createElement("span");
    this.panelTitle.className = "hud-dock-panel-title";
    const close = document.createElement("button");
    close.type = "button";
    close.className = "hud-dock-close";
    close.textContent = "\u00d7";
    close.setAttribute("aria-label", "Close panel");
    close.addEventListener("click", () => this.close(true));
    this.setPointerControls(close);
    header.append(this.panelTitle, close);

    this.panelBody = document.createElement("div");
    this.panelBody.className = "hud-dock-panel-body";
    this.panel.append(header, this.panelBody);

    this.element.append(this.bar, this.panel);
    root.append(this.element);
  }

  addPanel(options: DockPanelOptions): HTMLButtonElement {
    if (this.entries.has(options.id)) {
      throw new Error(`Duplicate HUD dock panel: ${options.id}`);
    }

    const button = this.createButton(options.label, options.icon, options.id);
    button.setAttribute("aria-expanded", "false");
    button.setAttribute("aria-controls", `hud-dock-content-${options.id}`);
    button.addEventListener("click", () => this.toggle(options.id));

    this.insertOrdered(button, options.order ?? 0);

    options.content.id = `hud-dock-content-${options.id}`;
    options.content.classList.add("hud-dock-content");
    options.content.hidden = true;
    this.panelBody.appendChild(options.content);
    this.entries.set(options.id, { options, button });
    return button;
  }

  /**
   * Registers a button that toggles external content (e.g. the chat window)
   * rather than opening a dock panel. It shares the entries rail and ordering
   * with the panel triggers so it stays grouped with them.
   */
  addToggle(options: DockToggleOptions): DockToggle {
    if (this.toggles.has(options.id)) {
      throw new Error(`Duplicate HUD dock toggle: ${options.id}`);
    }

    const button = this.createButton(options.label, options.icon, options.id);
    button.classList.add("hud-dock-toggle");
    button.setAttribute("aria-pressed", "false");
    this.insertOrdered(button, options.order ?? 0);

    let pressed = false;
    const handle: DockToggle = {
      button,
      setPressed: (value: boolean): void => {
        pressed = value;
        button.setAttribute("aria-pressed", String(value));
      }
    };
    button.addEventListener("click", () => {
      handle.setPressed(!pressed);
      options.onToggle?.(pressed);
    });

    this.toggles.set(options.id, handle);
    return handle;
  }

  /** Inserts a rail button by order, keeping panel triggers and toggles interleaved. */
  private insertOrdered(button: HTMLButtonElement, order: number): void {
    button.dataset.dockOrder = String(order);
    const next = Array.from(this.entriesHost.children).find(
      (child) => Number((child as HTMLElement).dataset.dockOrder ?? "0") > order
    );
    if (next === undefined) this.entriesHost.appendChild(button);
    else this.entriesHost.insertBefore(button, next);
  }

  addAction(options: DockActionOptions): HTMLButtonElement {
    const button = this.createButton(options.label, options.icon, null);
    button.classList.add("hud-dock-action-button");
    button.addEventListener("click", options.onClick);
    this.actionsHost.appendChild(button);
    return button;
  }

  toggle(id: string): void {
    if (this.activeId === id) this.close();
    else this.open(id);
  }

  open(id: string): void {
    const entry = this.entries.get(id);
    if (entry === undefined || this.activeId === id) return;

    if (this.activeId !== null) this.hideEntry(this.activeId);
    this.activeId = id;
    entry.button.setAttribute("aria-expanded", "true");
    entry.options.content.hidden = false;
    this.panelTitle.textContent = entry.options.label;
    this.panel.hidden = false;
    entry.options.onOpen?.();
  }

  close(restoreFocus = false): void {
    const id = this.activeId;
    if (id === null) return;

    this.hideEntry(id);
    this.activeId = null;
    this.panel.hidden = true;
    this.panelTitle.textContent = "";
    if (restoreFocus) this.entries.get(id)?.button.focus();
  }

  get isOpen(): boolean {
    return this.activeId !== null;
  }

  get openPanelId(): string | null {
    return this.activeId;
  }

  contains(target: Node): boolean {
    return this.element.contains(target);
  }

  setConnection(state: ConnectionState): void {
    const entry = this.entries.get("status");
    if (entry === undefined) return;
    entry.button.dataset.connection = state;
    entry.button.title = `Status: ${CONNECTION_TITLE[state]}`;
  }

  private hideEntry(id: string): void {
    const entry = this.entries.get(id);
    if (entry === undefined) return;

    entry.button.setAttribute("aria-expanded", "false");
    entry.options.content.hidden = true;
    entry.options.onClose?.();
  }

  private createButton(
    label: string,
    icon: DockIcon,
    panelId: string | null
  ): HTMLButtonElement {
    const button = document.createElement("button");
    button.type = "button";
    button.className = "hud-dock-button";
    button.setAttribute("aria-label", label);
    button.title = label;
    if (panelId !== null) button.dataset.dockId = panelId;

    const labelElement = document.createElement("span");
    labelElement.className = "hud-dock-label";
    labelElement.textContent = label;
    button.append(iconElement(icon), labelElement);
    this.setPointerControls(button);
    return button;
  }

  private setPointerControls(element: HTMLElement): void {
    element.dataset.hudControl = "true";
  }
}

function iconElement(name: DockIcon): SVGSVGElement {
  const svg = document.createElementNS(SVG_NS, "svg");
  svg.setAttribute("viewBox", "0 0 24 24");
  svg.setAttribute("aria-hidden", "true");
  svg.setAttribute("focusable", "false");
  svg.classList.add("hud-dock-icon");
  for (const path of ICONS[name]) {
    const element = document.createElementNS(SVG_NS, "path");
    element.setAttribute("d", path);
    svg.appendChild(element);
  }
  return svg;
}
