import { describe, expect, it, vi } from "vitest";
import type { IntentClient } from "../net/intent-client";
import { decodeMap } from "../simulation/map/rle-decoder";
import { validateMapSnapshot } from "../contracts/validate";
import mapFixture from "../test/fixtures/map-snapshot.json";
import { BuildingSelectionController } from "./building-selection";

const map = decodeMap(validateMapSnapshot(mapFixture));

function controller(overrides: { near?: boolean; useBuilding?: ReturnType<typeof vi.fn> } = {}) {
  const useBuilding =
    overrides.useBuilding ?? vi.fn(async () => ({ status: "sent" as const, result: { success: true } }));
  const changed = vi.fn();
  const notify = vi.fn();
  const selection = new BuildingSelectionController({
    getMap: () => map,
    getOwnSimTile: () => (overrides.near === false ? { x: 10, y: 10 } : { x: -6, y: -6 }),
    intent: { useBuilding } as unknown as IntentClient,
    onChanged: changed,
    notify
  });
  return { selection, useBuilding, changed, notify };
}

describe("BuildingSelectionController", () => {
  it("selects and reports changes", () => {
    const h = controller();
    const building = map.buildings.get(7001);
    if (building === undefined) throw new Error("missing building");
    h.selection.select(building);
    expect(h.selection.current).toBe(building);
    expect(h.changed).toHaveBeenCalledWith(building);
  });

  it("computes a proximity hint", () => {
    const near = controller({ near: true });
    const far = controller({ near: false });
    const building = map.buildings.get(7001);
    if (building === undefined) throw new Error("missing building");
    near.selection.select(building);
    far.selection.select(building);
    expect(near.selection.nearEntry()).toBe(true);
    expect(far.selection.nearEntry()).toBe(false);
  });

  it("submits a useBuilding intent", async () => {
    const h = controller();
    const building = map.buildings.get(7002);
    if (building === undefined) throw new Error("missing building");
    h.selection.select(building);
    await h.selection.interact();
    expect(h.useBuilding).toHaveBeenCalledWith(7002);
    expect(h.notify).toHaveBeenCalledWith("info", "building_requested", "Requested Foundry Office");
  });

  it("clears a removed selection and notifies", () => {
    const h = controller();
    const building = map.buildings.get(7001);
    if (building === undefined) throw new Error("missing building");
    h.selection.select(building);
    const gone = new Map(map.buildings);
    gone.delete(building.networkId);
    const removed = new BuildingSelectionController({
      getMap: () => ({ ...map, buildings: gone }),
      getOwnSimTile: () => null,
      intent: { useBuilding: h.useBuilding } as unknown as IntentClient,
      onChanged: h.changed,
      notify: h.notify
    });
    removed.select(building);
    removed.reconcile();
    expect(removed.current).toBeNull();
    expect(h.notify).toHaveBeenCalledWith("info", "building_removed", "Willow House is no longer there");
  });

  it("refuses to interact without a selection", async () => {
    const h = controller();
    await h.selection.interact();
    expect(h.useBuilding).not.toHaveBeenCalled();
  });
});
