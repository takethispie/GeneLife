import type { HudDock } from "../hud-dock";

export interface VenueOccupant {
  playerId: string;
  displayName: string | null;
  simNetworkId: number;
}

export interface VenueActivityNotice {
  playerId: string;
  displayName: string | null;
  activity: string;
  tick: number;
}

export interface VenueStatus {
  venueNetworkId: number;
  name: string;
  kind: string;
  occupancy: number;
  capacity: number;
  isOpen: boolean;
  isClosed: boolean;
  activities: string;
  occupants: VenueOccupant[];
  recentActivities: VenueActivityNotice[];
}

export interface VenueActionOutcome {
  status: string;
  message?: string;
}

export interface VenuePanelOptions {
  root: HTMLElement;
  apiBaseUrl: string;
  token: () => string | null;
  sendAction: (kind: string, payload: Record<string, unknown>) => Promise<VenueActionOutcome>;
  dock?: HudDock;
  fetchImpl?: typeof fetch;
  onError?: (message: string) => void;
}

const ACTIVITIES = ["Sit", "Talk", "ShareMeal", "PlayGame", "Cheer"] as const;

/**
 * Venue occupancy and activity bar. The panel is DOM-only and requests
 * activities through the tick-start player-action path.
 */
export class VenuePanel {
  private readonly options: VenuePanelOptions;
  private readonly root: HTMLElement;
  private readonly title: HTMLElement;
  private readonly summary: HTMLElement;
  private readonly occupants: HTMLUListElement;
  private readonly recent: HTMLUListElement;
  private readonly activityBar: HTMLDivElement;
  private readonly notice: HTMLElement;
  private readonly buttons = new Map<string, HTMLButtonElement>();
  private status: VenueStatus | null = null;
  private disposed = false;

  constructor(options: VenuePanelOptions) {
    this.options = options;
    this.root = document.createElement("section");
    this.root.className = "social-venue";
    this.root.setAttribute("aria-label", "Venue");

    const header = document.createElement("div");
    header.className = "social-venue-header";

    this.title = document.createElement("span");
    this.title.textContent = "Venue";
    const refresh = document.createElement("button");
    refresh.type = "button";
    refresh.textContent = "Refresh";
    refresh.addEventListener("click", () => void this.refresh());
    header.append(this.title, refresh);
    this.root.appendChild(header);

    this.summary = document.createElement("div");
    this.summary.className = "social-venue-summary";
    this.summary.textContent = "Not in a venue";
    this.root.appendChild(this.summary);

    this.occupants = document.createElement("ul");
    this.occupants.className = "social-venue-occupants";
    this.root.appendChild(this.occupants);

    this.recent = document.createElement("ul");
    this.recent.className = "social-venue-recent";
    this.root.appendChild(this.recent);

    this.activityBar = document.createElement("div");
    this.activityBar.className = "social-venue-activities";
    this.activityBar.setAttribute("role", "group");
    this.activityBar.setAttribute("aria-label", "Venue activities");
    for (const activity of ACTIVITIES) {
      const button = document.createElement("button");
      button.type = "button";
      button.dataset.activity = activity;
      button.textContent = activity;
      button.disabled = true;
      button.addEventListener("click", () => void this.perform(activity));
      this.activityBar.appendChild(button);
      this.buttons.set(activity, button);
    }
    this.root.appendChild(this.activityBar);

    this.notice = document.createElement("div");
    this.notice.className = "social-venue-notice";
    this.notice.setAttribute("role", "status");
    this.root.appendChild(this.notice);

    if (options.dock) {
      options.dock.addPanel({
        id: "venue",
        label: "Venue",
        icon: "pin",
        order: 40,
        content: this.root,
        onOpen: () => void this.refresh()
      });
    } else {
      options.root.appendChild(this.root);
    }
    void this.refresh();
  }

  async refresh(): Promise<void> {
    const token = this.options.token();
    if (token === null || this.disposed) return;

    try {
      const fetcher = this.options.fetchImpl ?? fetch;
      const response = await fetcher(`${this.options.apiBaseUrl}/api/social/venues/current`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (response.status === 204) {
        this.status = null;
        this.render();
        return;
      }
      if (!response.ok) return;

      const status = (await response.json()) as VenueStatus;
      if (status === null || typeof status !== "object") return;
      this.status = status;
      this.render();
    } catch (error) {
      this.options.onError?.(error instanceof Error ? error.message : "Venue status unavailable");
    }
  }

  dispose(): void {
    this.disposed = true;
    this.root.remove();
  }

  private render(): void {
    if (this.disposed) return;
    const status = this.status;
    if (status === null) {
      this.title.textContent = "Venue";
      this.summary.textContent = "Not in a venue";
      this.occupants.replaceChildren();
      this.recent.replaceChildren();
      for (const button of this.buttons.values()) button.disabled = true;
      return;
    }

    this.title.textContent = status.name;
    const opening = status.isClosed
      ? "closed"
      : status.isOpen
        ? "open"
        : "outside opening hours";
    this.summary.textContent = `${status.kind} · ${status.occupancy}/${status.capacity} · ${opening}`;

    this.occupants.replaceChildren();
    for (const occupant of status.occupants) {
      const row = document.createElement("li");
      row.textContent = occupant.displayName ?? "Resident";
      this.occupants.appendChild(row);
    }

    this.recent.replaceChildren();
    for (const activity of status.recentActivities.slice(-5)) {
      const row = document.createElement("li");
      row.textContent = `${activity.displayName ?? "Resident"} ${activity.activity.toLowerCase()}`;
      this.recent.appendChild(row);
    }

    const allowed = new Set(
      status.activities.split(",").map((value) => value.trim()).filter((value) => value.length > 0)
    );
    for (const [activity, button] of this.buttons) {
      button.disabled = !status.isOpen || status.isClosed || !allowed.has(activity);
    }
  }

  private async perform(activity: string): Promise<void> {
    try {
      const outcome = await this.options.sendAction("PerformVenueActivity", { activity });
      if (outcome.status !== "sent") {
        this.setNotice(outcome.message ?? "Activity not accepted.");
        return;
      }
      this.setNotice("");
      await this.refresh();
    } catch (error) {
      this.setNotice(error instanceof Error ? error.message : "Activity not accepted.");
    }
  }

  private setNotice(message: string): void {
    if (!this.disposed) this.notice.textContent = message;
  }
}
