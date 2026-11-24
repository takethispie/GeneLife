import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import type { MapBuilding } from "../contracts/hub";
import { Hud } from "./hud";
import { NotificationCenter } from "./notifications";
import { SettingsStore } from "./settings";
import { UiStore } from "./ui-store";

function harness() {
  const root = document.createElement("div");
  document.body.appendChild(root);
  const ui = new UiStore();
  const notifications = new NotificationCenter({ now: () => Date.now() });
  const settings = new SettingsStore(null);
  const actions = {
    onInteract: vi.fn(),
    onSignOut: vi.fn(),
    onRetry: vi.fn(),
    onSelectBuilding: vi.fn(),
    focusCanvas: vi.fn()
  };
  const hud = new Hud({ root, ui, notifications, settings, actions, updateIntervalMs: 100 });
  return { root, ui, notifications, settings, actions, hud };
}

function buttonByLabel(root: HTMLElement, label: string): HTMLButtonElement {
  const button = root.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
  if (button === null) throw new Error(`missing button ${label}`);
  return button;
}

const building: MapBuilding = {
  networkId: 1,
  kind: "office",
  name: "<img src=x onerror=alert(1)>",
  district: "<script>alert(2)</script>",
  x: 0,
  y: 0,
  width: 2,
  height: 2,
  entryX: 0,
  entryY: 1,
  ownerNetworkId: 7
};

describe("Hud", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    document.body.replaceChildren();
  });

  afterEach(() => {
    vi.useRealTimers();
    document.body.replaceChildren();
  });

  it("coalesces updates to the configured 10 Hz cadence", () => {
    const h = harness();
    h.ui.patch({ connection: "connecting" });
    vi.advanceTimersByTime(100);
    expect(h.root.querySelector('[data-field="connection"]')?.textContent).toBe(
      "Connecting…"
    );

    h.ui.patch({ connection: "connected" });
    vi.advanceTimersByTime(99);
    expect(h.root.querySelector('[data-field="connection"]')?.textContent).toBe(
      "Connecting…"
    );
    vi.advanceTimersByTime(1);
    expect(h.root.querySelector('[data-field="connection"]')?.textContent).toBe("Connected");
    h.hud.dispose();
  });

  it("renders server strings as text, never HTML", () => {
    const h = harness();
    h.ui.patch({
      playerId: "<img src=x onerror=alert(1)>",
      mapId: "<script>alert(2)</script>",
      selection: { kind: "building", building }
    });
    vi.advanceTimersByTime(100);

    expect(h.root.querySelectorAll("img")).toHaveLength(0);
    expect(h.root.querySelectorAll("script")).toHaveLength(0);
    expect(h.root.querySelector('[data-field="player"]')?.textContent).toBe(
      "<img src=x onerror=alert(1)>"
    );
    expect(h.root.querySelector(".selection-name")?.textContent).toBe(
      "<img src=x onerror=alert(1)>"
    );
    h.hud.dispose();
  });

  it("shows selection details and toggles the interact action", () => {
    const h = harness();
    h.ui.patch({ selection: { kind: "building", building } });
    vi.advanceTimersByTime(100);
    const panel = h.root.querySelector(".selection");
    expect(panel?.hasAttribute("hidden")).toBe(false);
    const interact = Array.from(h.root.querySelectorAll("button")).find(
      (button) => button.textContent === "Interact"
    );
    interact?.click();
    expect(h.actions.onInteract).toHaveBeenCalledTimes(1);

    h.ui.patch({ selection: null });
    vi.advanceTimersByTime(100);
    expect(panel?.hasAttribute("hidden")).toBe(true);
    h.hud.dispose();
  });

  it("announces errors through the live region and hides the empty panel", () => {
    const h = harness();
    expect(h.root.querySelector(".hud-bottom-right")?.hasAttribute("hidden")).toBe(true);
    expect(h.root.querySelector(".hud-bottom-left")?.hasAttribute("hidden")).toBe(true);

    h.notifications.push("error", "intent_blocked", "That tile is blocked");
    vi.advanceTimersByTime(100);
    expect(h.root.querySelector('[role="status"]')?.textContent).toBe("That tile is blocked");
    expect(h.root.querySelectorAll(".notification")).toHaveLength(1);
    expect(h.root.querySelector(".hud-bottom-left")?.hasAttribute("hidden")).toBe(false);

    h.notifications.clear();
    vi.advanceTimersByTime(100);
    expect(h.root.querySelectorAll(".notification")).toHaveLength(0);
    expect(h.root.querySelector(".hud-bottom-left")?.hasAttribute("hidden")).toBe(true);
    h.hud.dispose();
  });

  it("keeps controls in one top-right toolbar and opens one panel at a time", () => {
    const h = harness();
    expect(h.root.querySelector(".hud-top-left")).toBeNull();
    expect(h.root.querySelector(".hud-top-right")).toBeNull();

    h.ui.patch({ connection: "connected" });
    vi.advanceTimersByTime(100);
    expect(h.root.querySelector('[data-field="connection"]')?.textContent).toBe("Connected");
    expect(h.root.querySelector('[data-dock-id="status"]')?.getAttribute("data-connection")).toBe(
      "connected"
    );

    const status = buttonByLabel(h.root, "Status");
    const settings = buttonByLabel(h.root, "Settings");
    status.click();
    expect(h.root.querySelector(".hud-status")?.hasAttribute("hidden")).toBe(false);
    expect(h.root.querySelector(".hud-settings")?.hasAttribute("hidden")).toBe(true);

    settings.click();
    expect(h.root.querySelector(".hud-status")?.hasAttribute("hidden")).toBe(true);
    expect(h.root.querySelector(".hud-settings")?.hasAttribute("hidden")).toBe(false);

    settings.click();
    expect(h.root.querySelector(".hud-settings")?.hasAttribute("hidden")).toBe(true);
    h.hud.dispose();
  });

  it("opens settings and applies controls", () => {
    const h = harness();
    const settingsButton = Array.from(h.root.querySelectorAll("button")).find(
      (button) => button.textContent === "Settings"
    );
    settingsButton?.click();
    expect(h.root.querySelector(".hud-settings")?.hasAttribute("hidden")).toBe(false);

    const reduce = h.root.querySelector<HTMLInputElement>('input[type="checkbox"]');
    if (reduce === null) throw new Error("missing reduce motion input");
    reduce.checked = true;
    reduce.dispatchEvent(new Event("change"));
    expect(h.settings.settings.reduceMotion).toBe(true);
    h.hud.dispose();
  });
});
