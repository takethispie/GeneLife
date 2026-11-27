import { describe, expect, it, vi } from "vitest";
import type { Camera } from "../render/camera";
import { DEFAULT_BINDINGS, type ClientSettings } from "../ui/settings";
import { CameraInput } from "./camera-input";

function harness() {
  const canvas = document.createElement("canvas");
  canvas.getBoundingClientRect = () =>
    ({ left: 0, top: 0, width: 800, height: 600, right: 800, bottom: 600, x: 0, y: 0 } as DOMRect);
  const camera = {
    zoom: 1,
    setZoom: vi.fn(),
    zoomAt: vi.fn(),
    panByTiles: vi.fn()
  } as unknown as Camera;
  const input = new CameraInput(
    {
      canvas,
      camera,
      getSettings: () => ({ bindings: DEFAULT_BINDINGS } as unknown as ClientSettings),
      onCancel: vi.fn(),
      onToggleDebug: vi.fn()
    },
    () => () => {}
  );
  return { canvas, camera, input };
}

describe("CameraInput", () => {
  it("suppresses the native context menu on the canvas", () => {
    const h = harness();
    const event = new MouseEvent("contextmenu", { bubbles: true, cancelable: true });
    h.canvas.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(true);
    h.input.dispose();
  });

  it("does not suppress the context menu over the HUD", () => {
    const h = harness();
    const hud = document.createElement("div");
    hud.id = "hud";
    const control = document.createElement("button");
    hud.appendChild(control);
    h.canvas.appendChild(hud);
    const event = new MouseEvent("contextmenu", { bubbles: true, cancelable: true });
    control.dispatchEvent(event);
    expect(event.defaultPrevented).toBe(false);
    h.input.dispose();
  });

  it("a plain right-click starts no drag", () => {
    const h = harness();
    h.canvas.dispatchEvent(new MouseEvent("pointerdown", { button: 2, clientX: 100, clientY: 100 }));
    window.dispatchEvent(new MouseEvent("pointermove", { clientX: 160, clientY: 100 }));
    expect(h.camera.panByTiles).not.toHaveBeenCalled();
    h.input.dispose();
  });

  it("middle-drag pans the camera", () => {
    const h = harness();
    h.canvas.dispatchEvent(new MouseEvent("pointerdown", { button: 1, clientX: 100, clientY: 100 }));
    window.dispatchEvent(new MouseEvent("pointermove", { clientX: 140, clientY: 100 }));
    expect(h.camera.panByTiles).toHaveBeenCalled();
    h.input.dispose();
  });

  it("alt+left drag pans and a plain left click does not", () => {
    const h = harness();
    h.canvas.dispatchEvent(new MouseEvent("pointerdown", { button: 0, clientX: 100, clientY: 100 }));
    window.dispatchEvent(new MouseEvent("pointermove", { clientX: 140, clientY: 100 }));
    expect(h.camera.panByTiles).not.toHaveBeenCalled();

    h.canvas.dispatchEvent(
      new MouseEvent("pointerdown", { button: 0, altKey: true, clientX: 100, clientY: 100 })
    );
    window.dispatchEvent(new MouseEvent("pointermove", { clientX: 140, clientY: 100 }));
    expect(h.camera.panByTiles).toHaveBeenCalled();
    h.input.dispose();
  });

  it("the wheel zooms and never scrolls the page", () => {
    const h = harness();
    const event = new WheelEvent("wheel", { deltaY: -100, clientX: 400, clientY: 300, cancelable: true });
    h.canvas.dispatchEvent(event);
    expect(h.camera.zoomAt).toHaveBeenCalled();
    expect(event.defaultPrevented).toBe(true);
    h.input.dispose();
  });
});
