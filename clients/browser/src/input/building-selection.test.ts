import { afterEach, describe, expect, it, vi } from "vitest";
import type { MapBuilding } from "../contracts/hub";
import type { IntentClient } from "../net/intent-client";
import { decodeMap } from "../simulation/map/rle-decoder";
import { validateMapSnapshot } from "../contracts/validate";
import type { NotificationKind } from "../ui/notifications";
import mapFixture from "../test/fixtures/map-snapshot.json";
import { BuildingSelectionController } from "./building-selection";

const map = decodeMap(validateMapSnapshot(mapFixture));

type Tile = { x: number; y: number };
type Changed = (building: MapBuilding | null) => void;
type Notify = (kind: NotificationKind, code: string, message: string) => void;

interface Harness {
  selection: BuildingSelectionController;
  moveTo: ReturnType<typeof vi.fn>;
  useBuilding: ReturnType<typeof vi.fn>;
  changed: ReturnType<typeof vi.fn<Changed>>;
  notify: ReturnType<typeof vi.fn<Notify>>;
  setTile: (tile: Tile | null) => void;
}

function harness(
  overrides: {
    tile?: Tile | null;
    moveTo?: ReturnType<typeof vi.fn>;
    useBuilding?: ReturnType<typeof vi.fn>;
    walkTimeoutMs?: number;
  } = {}
): Harness {
  let tile: Tile | null = overrides.tile ?? { x: 10, y: 10 };
  const moveTo =
    overrides.moveTo ?? vi.fn(async () => ({ status: "sent" as const, result: { success: true } }));
  const useBuilding =
    overrides.useBuilding ?? vi.fn(async () => ({ status: "sent" as const, result: { success: true } }));
  const changed = vi.fn<Changed>();
  const notify = vi.fn<Notify>();
  const selection = new BuildingSelectionController({
    getMap: () => map,
    getOwnGenosTile: () => tile,
    intent: { moveTo, useBuilding } as unknown as IntentClient,
    onChanged: changed,
    notify,
    walkTimeoutMs: overrides.walkTimeoutMs ?? 15_000
  });
  return {
    selection,
    moveTo,
    useBuilding,
    changed,
    notify,
    setTile: (next) => {
      tile = next;
    }
  };
}

function building(id: number) {
  const found = map.buildings.get(id);
  if (found === undefined) throw new Error(`missing building ${id}`);
  return found;
}

afterEach(() => {
  vi.useRealTimers();
});

describe("BuildingSelectionController", () => {
  it("selects and reports changes", () => {
    const h = harness();
    const b = building(7001);
    h.selection.select(b);
    expect(h.selection.current).toBe(b);
    expect(h.changed).toHaveBeenCalledWith(b);
  });

  it("computes a proximity hint", () => {
    const near = harness({ tile: { x: -6, y: -6 } });
    const far = harness({ tile: { x: 10, y: 10 } });
    near.selection.select(building(7001));
    far.selection.select(building(7001));
    expect(near.selection.nearEntry()).toBe(true);
    expect(far.selection.nearEntry()).toBe(false);
  });

  it("interacts immediately when already in range, with no walk", async () => {
    const h = harness({ tile: { x: 3, y: 6 } }); // building 7002 entry
    h.selection.select(building(7002));
    await h.selection.interact();
    expect(h.useBuilding).toHaveBeenCalledTimes(1);
    expect(h.useBuilding).toHaveBeenCalledWith(7002);
    expect(h.moveTo).not.toHaveBeenCalled();
    expect(h.notify).toHaveBeenCalledWith("info", "building_requested", "Requested Foundry Office");
  });

  it("walks to the entrance when out of range, then interacts on arrival", async () => {
    const h = harness({ tile: { x: 10, y: 10 } });
    h.selection.select(building(7001)); // entry (-5, -4)

    await h.selection.interact();
    expect(h.moveTo).toHaveBeenCalledTimes(1);
    expect(h.moveTo).toHaveBeenCalledWith(-5, -4);
    expect(h.useBuilding).not.toHaveBeenCalled();
    expect(h.notify).toHaveBeenCalledWith(
      "info",
      "walking_to_entrance",
      "Walking to Willow House entrance"
    );

    // Arrival: the server snapshot moves the genoses next to the entrance.
    h.setTile({ x: -5, y: -4 });
    h.selection.reconcile();
    expect(h.useBuilding).toHaveBeenCalledTimes(1);
    expect(h.useBuilding).toHaveBeenCalledWith(7001);

    // Reconciles on later snapshots must not fire the interaction again.
    h.selection.reconcile();
    h.selection.reconcile();
    expect(h.useBuilding).toHaveBeenCalledTimes(1);
  });

  it("cancels the queued interact when another building is selected", async () => {
    const h = harness({ tile: { x: 10, y: 10 } });
    h.selection.select(building(7001));
    await h.selection.interact();
    expect(h.moveTo).toHaveBeenCalledTimes(1);

    h.selection.select(building(7002));
    h.setTile({ x: -5, y: -4 }); // near the old building's entrance
    h.selection.reconcile();
    expect(h.useBuilding).not.toHaveBeenCalled();
  });

  it("cancels the queued interact when the player issues another move", async () => {
    const h = harness({ tile: { x: 10, y: 10 } });
    h.selection.select(building(7001));
    await h.selection.interact();
    expect(h.moveTo).toHaveBeenCalledTimes(1);

    h.selection.cancelPendingInteract(); // the targeting layer's onPlayerMove
    h.setTile({ x: -5, y: -4 });
    h.selection.reconcile();
    expect(h.useBuilding).not.toHaveBeenCalled();
  });

  it("surfaces a message when the entrance walk is refused", async () => {
    const moveTo = vi.fn(async () => ({ status: "failed" as const, message: "That tile is blocked" }));
    const h = harness({ tile: { x: 10, y: 10 }, moveTo });
    h.selection.select(building(7001));

    await h.selection.interact();
    expect(h.notify).toHaveBeenCalledWith(
      "error",
      "walk_failed",
      "Could not reach Willow House entrance"
    );
    // A refused walk must not leave a queued interaction behind.
    h.setTile({ x: -5, y: -4 });
    h.selection.reconcile();
    expect(h.useBuilding).not.toHaveBeenCalled();
  });

  it("surfaces a message when the entrance walk times out", async () => {
    vi.useFakeTimers();
    const h = harness({ tile: { x: 10, y: 10 }, walkTimeoutMs: 1_000 });
    h.selection.select(building(7001));

    await h.selection.interact();
    expect(h.moveTo).toHaveBeenCalledTimes(1);
    vi.advanceTimersByTime(1_000);
    expect(h.notify).toHaveBeenCalledWith(
      "warning",
      "walk_timeout",
      "Could not reach Willow House entrance"
    );

    h.setTile({ x: -5, y: -4 });
    h.selection.reconcile();
    expect(h.useBuilding).not.toHaveBeenCalled();
  });

  it("clears a removed selection and notifies", () => {
    const h = harness();
    const b = building(7001);
    h.selection.select(b);
    const gone = new Map(map.buildings);
    gone.delete(b.networkId);
    const removed = new BuildingSelectionController({
      getMap: () => ({ ...map, buildings: gone }),
      getOwnGenosTile: () => null,
      intent: { useBuilding: h.useBuilding } as unknown as IntentClient,
      onChanged: (next) => h.changed(next),
      notify: (kind, code, message) => h.notify(kind, code, message)
    });
    removed.select(b);
    removed.reconcile();
    expect(removed.current).toBeNull();
    expect(h.notify).toHaveBeenCalledWith("info", "building_removed", "Willow House is no longer there");
  });

  it("refuses to interact without a selection", async () => {
    const h = harness();
    await h.selection.interact();
    expect(h.useBuilding).not.toHaveBeenCalled();
    expect(h.moveTo).not.toHaveBeenCalled();
  });
});
