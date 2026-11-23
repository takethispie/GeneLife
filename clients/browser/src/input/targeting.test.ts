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

function click(canvas: HTMLCanvasElement): void {
  canvas.dispatchEvent(new MouseEvent("click", { button: 0, clientX: 400, clientY: 300 }));
}

describe("TargetingController", () => {
  it("submits a move intent for a walkable tile", async () => {
    const h = harness();
    h.setTile({ x: 0, y: 0 });
    click(h.canvas);
    await Promise.resolve();
    expect(h.moveTo).toHaveBeenCalledWith(0, 0);
    expect(h.onTarget).toHaveBeenCalledWith({ x: 0, y: 0 });
    expect(h.onSelectBuilding).not.toHaveBeenCalled();
    h.targeting.dispose();
  });

  it("selects a building instead of moving onto it", () => {
    const h = harness();
    h.setTile({ x: -6, y: -6 });
    click(h.canvas);
    expect(h.moveTo).not.toHaveBeenCalled();
    const selected = h.onSelectBuilding.mock.calls[0]?.[0] as MapBuilding | null;
    expect(selected?.networkId).toBe(7001);
    h.targeting.dispose();
  });

  it("rejects water without an intent", () => {
    const h = harness();
    h.setTile({ x: -15, y: -15 });
    click(h.canvas);
    expect(h.moveTo).not.toHaveBeenCalled();
    expect(h.notify).toHaveBeenCalledWith("warning", "blocked_tile", "That tile is blocked");
    h.targeting.dispose();
  });

  it("rejects targets outside the map", () => {
    const h = harness();
    h.setTile({ x: 9_999, y: 9_999 });
    click(h.canvas);
    expect(h.moveTo).not.toHaveBeenCalled();
    expect(h.notify).toHaveBeenCalledWith("info", "outside_map", "That is outside the map");
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
