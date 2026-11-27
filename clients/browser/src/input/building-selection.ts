import type { MapBuilding } from "../contracts/hub";
import type { IntentClient } from "../net/intent-client";
import type { DecodedMap } from "../simulation/map/rle-decoder";
import type { NotificationKind } from "../ui/notifications";

export interface BuildingSelectionOptions {
  getMap: () => DecodedMap | null;
  getOwnGenosTile: () => { x: number; y: number } | null;
  intent: IntentClient;
  onChanged: (building: MapBuilding | null) => void;
  notify: (kind: NotificationKind, code: string, message: string) => void;
  /** UX hint only; the server owns the real entry-distance check. */
  nearEntryRadius?: number;
  /** How long to wait for the walk to the entrance before giving up. */
  walkTimeoutMs?: number;
}

interface PendingInteract {
  buildingNetworkId: number;
  name: string;
}

/**
 * Holds the selected building and submits `useBuilding` intents. The client never
 * asserts range, role or ownership: it shows a proximity hint and reports the
 * server's answer honestly.
 *
 * When the selected genoses are out of range, `interact` walks them to the
 * building's entrance (`entryX`, `entryY`) and fires the interaction once they
 * arrive. The walk is a pending action: re-selecting another building, issuing
 * another move, or the walk failing or timing out cancels it, so a queued
 * interaction can never fire late or repeatedly. The server still owns the
 * authoritative range check; a refusal is surfaced to the player.
 */
export class BuildingSelectionController {
  private selected: MapBuilding | null = null;
  private readonly nearEntryRadius: number;
  private readonly walkTimeoutMs: number;
  private pending: PendingInteract | null = null;
  private walkTimer: ReturnType<typeof setTimeout> | null = null;

  constructor(private readonly options: BuildingSelectionOptions) {
    this.nearEntryRadius = options.nearEntryRadius ?? 2;
    this.walkTimeoutMs = options.walkTimeoutMs ?? 15_000;
  }

  get current(): MapBuilding | null {
    return this.selected;
  }

  select(building: MapBuilding | null): void {
    if (building?.networkId !== this.selected?.networkId) this.cancelPendingInteract();
    this.selected = building;
    this.options.onChanged(building);
  }

  /**
   * Reconciles the selection with the current map: clears removed buildings,
   * refreshes details when a delta replaced the building object, and fires a
   * queued interaction once the genoses have reached the entrance.
   */
  reconcile(): void {
    const selected = this.selected;
    if (selected !== null) {
      const map = this.options.getMap();
      const current = map?.buildings.get(selected.networkId);
      if (current === undefined) {
        const name = selected.name;
        this.selected = null;
        this.cancelPendingInteract();
        this.options.onChanged(null);
        this.options.notify("info", "building_removed", `${name} is no longer there`);
        return;
      }
      if (current !== selected) {
        this.selected = current;
        this.options.onChanged(current);
      }
    }
    this.checkPendingArrival();
  }

  nearEntry(): boolean {
    const building = this.selected;
    const genosTile = this.options.getOwnGenosTile();
    if (building === null || genosTile === null) return false;
    return (
      Math.max(Math.abs(genosTile.x - building.entryX), Math.abs(genosTile.y - building.entryY)) <=
      this.nearEntryRadius
    );
  }

  async interact(): Promise<void> {
    const building = this.selected;
    if (building === null) return;

    const map = this.options.getMap();
    if (map === null || !map.buildings.has(building.networkId)) {
      this.reconcile();
      return;
    }

    if (this.nearEntry()) {
      this.cancelPendingInteract();
      await this.submitInteract(building);
      return;
    }

    // A walk to this same entrance is already queued; do not spam another move.
    if (this.pending?.buildingNetworkId === building.networkId) return;
    this.startWalk(building);
  }

  /**
   * Cancels a queued out-of-range interaction. Called when the player picks a
   * different target or issues another move, so the interaction fires only for
   * the action the player last asked for.
   */
  cancelPendingInteract(): void {
    if (this.walkTimer !== null) {
      clearTimeout(this.walkTimer);
      this.walkTimer = null;
    }
    this.pending = null;
  }

  private startWalk(building: MapBuilding): void {
    this.cancelPendingInteract();
    const pending: PendingInteract = {
      buildingNetworkId: building.networkId,
      name: building.name
    };
    this.pending = pending;
    this.walkTimer = setTimeout(() => this.onWalkTimeout(pending), this.walkTimeoutMs);
    this.options.notify("info", "walking_to_entrance", `Walking to ${building.name} entrance`);
    void this.requestEntranceWalk(building, pending);
  }

  private async requestEntranceWalk(building: MapBuilding, pending: PendingInteract): Promise<void> {
    const outcome = await this.options.intent.moveTo(building.entryX, building.entryY);
    // A superseding action already cancelled this walk; do not report for it.
    if (this.pending !== pending) return;
    if (outcome.status === "sent") return;
    this.cancelPendingInteract();
    if (outcome.status === "failed") {
      this.options.notify("error", "walk_failed", `Could not reach ${building.name} entrance`);
    } else {
      this.options.notify("warning", "intent_not_ready", outcome.message);
    }
  }

  private checkPendingArrival(): void {
    const pending = this.pending;
    if (pending === null) return;
    const building = this.selected;
    if (building === null || building.networkId !== pending.buildingNetworkId) {
      this.cancelPendingInteract();
      return;
    }
    if (!this.nearEntry()) return;
    this.cancelPendingInteract();
    void this.submitInteract(building);
  }

  private onWalkTimeout(pending: PendingInteract): void {
    if (this.pending !== pending) return;
    this.cancelPendingInteract();
    this.options.notify("warning", "walk_timeout", `Could not reach ${pending.name} entrance`);
  }

  private async submitInteract(building: MapBuilding): Promise<void> {
    const outcome = await this.options.intent.useBuilding(building.networkId);
    if (outcome.status === "sent") {
      this.options.notify("info", "building_requested", `Requested ${building.name}`);
    } else if (outcome.status === "rate-limited" || outcome.status === "not-ready") {
      this.options.notify("warning", "intent_not_ready", outcome.message);
    } else {
      this.options.notify("error", "intent_rejected", outcome.message);
    }
  }
}
