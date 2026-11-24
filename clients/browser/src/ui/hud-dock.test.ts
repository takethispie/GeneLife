import { afterEach, describe, expect, it, vi } from "vitest";
import { HudDock } from "./hud-dock";

function harness() {
  const root = document.createElement("div");
  document.body.appendChild(root);
  const dock = new HudDock(root);
  const first = panelContent("First panel");
  const second = panelContent("Second panel");
  const onOpen = vi.fn();
  const onClose = vi.fn();
  dock.addPanel({
    id: "first",
    label: "First",
    icon: "status",
    order: 10,
    content: first,
    onOpen,
    onClose
  });
  dock.addPanel({
    id: "second",
    label: "Second",
    icon: "settings",
    order: 20,
    content: second
  });
  return { root, dock, first, second, onOpen, onClose };
}

function panelContent(text: string): HTMLElement {
  const element = document.createElement("div");
  element.textContent = text;
  return element;
}

function buttonByLabel(root: HTMLElement, label: string): HTMLButtonElement {
  const button = root.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
  if (button === null) throw new Error(`missing button ${label}`);
  return button;
}

describe("HudDock", () => {
  afterEach(() => {
    document.body.replaceChildren();
    vi.restoreAllMocks();
  });

  it("opens one panel at a time and marks the trigger expanded", () => {
    const h = harness();
    expect(h.first.hidden).toBe(true);
    expect(h.second.hidden).toBe(true);

    buttonByLabel(h.root, "First").click();
    expect(h.dock.isOpen).toBe(true);
    expect(h.dock.openPanelId).toBe("first");
    expect(h.first.hidden).toBe(false);
    expect(buttonByLabel(h.root, "First").getAttribute("aria-expanded")).toBe("true");
    expect(h.onOpen).toHaveBeenCalledTimes(1);

    buttonByLabel(h.root, "Second").click();
    expect(h.first.hidden).toBe(true);
    expect(h.second.hidden).toBe(false);
    expect(h.onClose).toHaveBeenCalledTimes(1);
    expect(buttonByLabel(h.root, "First").getAttribute("aria-expanded")).toBe("false");
    expect(buttonByLabel(h.root, "Second").getAttribute("aria-expanded")).toBe("true");
  });

  it("closes the active panel from its trigger and restores focus", () => {
    const h = harness();
    buttonByLabel(h.root, "First").click();
    buttonByLabel(h.root, "First").click();

    expect(h.dock.isOpen).toBe(false);
    expect(h.first.hidden).toBe(true);
    expect(h.onClose).toHaveBeenCalledTimes(1);

    h.dock.open("first");
    h.dock.close(true);
    expect(document.activeElement).toBe(buttonByLabel(h.root, "First"));
  });

  it("runs toolbar actions without opening a panel", () => {
    const h = harness();
    const onClick = vi.fn();
    const action = h.dock.addAction({ label: "Leave", icon: "signout", onClick });
    action.click();

    expect(onClick).toHaveBeenCalledTimes(1);
    expect(h.dock.isOpen).toBe(false);
  });

  it("orders panel triggers by order value", () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const dock = new HudDock(root);
    dock.addPanel({ id: "late", label: "Late", icon: "pin", order: 20, content: panelContent("late") });
    dock.addPanel({ id: "early", label: "Early", icon: "chat", order: 10, content: panelContent("early") });

    const labels = Array.from(root.querySelectorAll(".hud-dock-button")).map((button) =>
      button.getAttribute("aria-label")
    );
    expect(labels).toEqual(["Early", "Late"]);
  });
});
