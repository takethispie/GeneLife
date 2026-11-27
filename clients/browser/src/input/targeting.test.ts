import { describe, expect, it, vi } from "vitest";
import type { MapBuilding } from "../contracts/hub";
import type { IntentClient } from "../net/intent-client";
import type { Camera } from "../render/camera";
import { decodeMap } from "../simulation/map/rle-decoder";
import { validateMapSnapshot } from "../contracts/validate";
import mapFixture from "../test/fixtures/map-snapshot.json";
import { TargetingController } from "./targeting";

const map = decodeMap(validateMapSnapshot(mapFixture));

function harness() {
  const canvas = document.createElement("canvas");
  canvas.getBoundingClientRect = () =>
    ({ left: 0, top: 0, width: 800, height: 600, right: 800, bottom: 600, x: 0, y: 0 } as DOMRect);
  let nextTile = { x: 0, y: 0 };
  const camera = {
    tileFromScreen: () => nextTile
  } as unknown as Camera;
  const moveTo = vi.fn(async () => ({ status: "sent" as const, result: { success: true } }));
  const intent = { moveTo } as unknown as IntentClient;
  const onHover = vi.fn();
  const onTarget = vi.fn();
  const onSelectBuilding = vi.fn();
  const notify = vi.fn();
  const targeting = new TargetingController({
    canvas,
    camera,
    getMap: () => map,
    intent,
    onHover,
    onTarget,
    onSelectBuilding,
    notify
  });

  return {
    canvas,
    targeting,
    moveTo,
    onHover,
    onTarget,
    onSelectBuilding,
    notify,
    setTile: (tile: { x: number; y: number }) => {
      nextTile = tile;
    }
  };
}

/** Left mouse button — selection only. */
function leftClick(canvas: HTMLCanvasElement): void {
  canvas.dispatchEvent(new MouseEvent("click", { button: 0, clientX: 400, clientY: 300 }));
}

/** Right mouse button — the click-to-move gesture. */
function rightClick(canvas: HTMLCanvasElement): void {
  canvas.dispatchEvent(new MouseEvent("pointerdown", { button: 2, clientX: 400, clientY: 300 }));
}

describe("TargetingController", () => {
  it("right-click submits exactly one move intent for a walkable tile", async () => {
    const h = harness();
    h.setTile({ x: 0, y: 0 });
    rightClick(h.canvas);
    await Promise.resolve();
    expect(h.moveTo).toHaveBeenCalledTimes(1);
    expect(h.moveTo).toHaveBeenCalledWith(0, 0);
    expect(h.onTarget).toHaveBeenCalledWith({ x: 0, y: 0 });
    expect(h.onSelectBuilding).not.toHaveBeenCalled();
    h.targeting.dispose();
  });

  it("left-click never submits a move", async () => {
    const h = harness();
    h.setTile({ x: 0, y: 0 });
    leftClick(h.canvas);
    await Promise.resolve();
    expect(h.moveTo).not.toHaveBeenCalled();
    expect(h.onSelectBuilding).not.toHaveBeenCalled();
    h.targeting.dispose();
  });

  it("left-click selects a building instead of moving onto it", async () => {
    const h = harness();
    h.setTile({ x: -6, y: -6 });
    leftClick(h.canvas);
    await Promise.resolve();
    expect(h.moveTo).not.toHaveBeenCalled();
    const selected = h.onSelectBuilding.mock.calls[0]?.[0] as MapBuilding | null;
    expect(selected?.networkId).toBe(7001);
    h.targeting.dispose();
  });

  it("right-click on a building tile does not move", async () => {
    const h = harness();
    h.setTile({ x: -6, y: -6 });
    rightClick(h.canvas);
    await Promise.resolve();
    expect(h.moveTo).not.toHaveBeenCalled();
    expect(h.onSelectBuilding).not.toHaveBeenCalled();
    expect(h.notify).toHaveBeenCalledWith("warning", "blocked_tile", "That tile is blocked");
    h.targeting.dispose();
  });

  it("right-click rejects water without an intent", async () => {
    const h = harness();
    h.setTile({ x: -15, y: -15 });
    rightClick(h.canvas);
    await Promise.resolve();
    expect(h.moveTo).not.toHaveBeenCalled();
    expect(h.notify).toHaveBeenCalledWith("warning", "blocked_tile", "That tile is blocked");
    h.targeting.dispose();
  });

  it("right-click outside the map explains itself", async () => {
    const h = harness();
    h.setTile({ x: 9_999, y: 9_999 });
    rightClick(h.canvas);
    await Promise.resolve();
    expect(h.moveTo).not.toHaveBeenCalled();
    expect(h.notify).toHaveBeenCalledWith("info", "outside_map", "That is outside the map");
    h.targeting.dispose();
  });

  it("left-click outside the map explains itself without moving", async () => {
    const h = harness();
    h.setTile({ x: 9_999, y: 9_999 });
    leftClick(h.canvas);
    await Promise.resolve();
    expect(h.moveTo).not.toHaveBeenCalled();
    expect(h.notify).toHaveBeenCalledWith("info", "outside_map", "That is outside the map");
    h.targeting.dispose();
  });

  it("right-click over the HUD does nothing", async () => {
    const h = harness();
    const hud = document.createElement("div");
    hud.id = "hud";
    const control = document.createElement("button");
    hud.appendChild(control);
    h.canvas.appendChild(hud);
    h.setTile({ x: 0, y: 0 });
    control.dispatchEvent(
      new MouseEvent("pointerdown", { button: 2, bubbles: true, clientX: 400, clientY: 300 })
    );
    await Promise.resolve();
    expect(h.moveTo).not.toHaveBeenCalled();
    h.targeting.dispose();
  });

  it("touch tap selects a building under the pointer", async () => {
    const h = harness();
    h.setTile({ x: -6, y: -6 });
    h.targeting.activateAtScreenPoint(400, 300);
    await Promise.resolve();
    expect(h.moveTo).not.toHaveBeenCalled();
    const selected = h.onSelectBuilding.mock.calls[0]?.[0] as MapBuilding | null;
    expect(selected?.networkId).toBe(7001);
    h.targeting.dispose();
  });

  it("touch tap on open ground moves", async () => {
    const h = harness();
    h.setTile({ x: 1, y: 1 });
    h.targeting.activateAtScreenPoint(400, 300);
    await Promise.resolve();
    expect(h.moveTo).toHaveBeenCalledTimes(1);
    expect(h.moveTo).toHaveBeenCalledWith(1, 1);
    h.targeting.dispose();
  });

  it("updates hover state and can activate with Enter", async () => {
    const h = harness();
    h.setTile({ x: 1, y: 1 });
    h.canvas.dispatchEvent(new MouseEvent("pointermove", { clientX: 10, clientY: 10 }));
    expect(h.onHover).toHaveBeenCalledWith({ x: 1, y: 1 }, true);

    h.canvas.dispatchEvent(new MouseEvent("pointermove", { clientX: 11, clientY: 11 }));
    expect(h.onHover).toHaveBeenCalledTimes(1);

    window.dispatchEvent(new KeyboardEvent("keydown", { key: "Enter" }));
    await Promise.resolve();
    expect(h.moveTo).toHaveBeenCalledWith(1, 1);
    h.targeting.dispose();
  });

  it("clears target and selection", () => {
    const h = harness();
    h.targeting.clearTarget();
    expect(h.onTarget).toHaveBeenCalledWith(null);
    h.targeting.clearAll();
    expect(h.onSelectBuilding).toHaveBeenCalledWith(null);
    h.targeting.dispose();
  });
});
