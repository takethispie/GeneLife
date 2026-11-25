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
}

/**
 * Holds the selected building and submits `useBuilding` intents. The client never
 * asserts range, role or ownership: it shows a proximity hint and reports the
 * server's answer honestly.
 */
export class BuildingSelectionController {
  private selected: MapBuilding | null = null;
  private readonly nearEntryRadius: number;

  constructor(private readonly options: BuildingSelectionOptions) {
    this.nearEntryRadius = options.nearEntryRadius ?? 2;
  }

  get current(): MapBuilding | null {
    return this.selected;
  }

  select(building: MapBuilding | null): void {
    this.selected = building;
    this.options.onChanged(building);
  }

  /**
   * Reconciles the selection with the current map: clears removed buildings and
   * refreshes details when a delta replaced the building object.
   */
  reconcile(): void {
    if (this.selected === null) return;
    const map = this.options.getMap();
    const current = map?.buildings.get(this.selected.networkId);
    if (current === undefined) {
      const name = this.selected.name;
      this.selected = null;
      this.options.onChanged(null);
      this.options.notify("info", "building_removed", `${name} is no longer there`);
      return;
    }
    if (current !== this.selected) {
      this.selected = current;
      this.options.onChanged(current);
    }
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
