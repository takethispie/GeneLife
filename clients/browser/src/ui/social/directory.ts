import type { HudDock } from "../hud-dock";

export interface DirectoryPresence {
  playerId: string;
  displayName: string | null;
  genosNetworkId: number;
  tileX: number;
  tileY: number;
  buildingNetworkId: number | null;
  state: string;
  appearance?: Record<string, number> | null;
  lastSeenTick: number;
}

export interface DirectoryResponse {
  scope: string;
  radius: number;
  items: DirectoryPresence[];
}

export interface PlayerDirectoryOptions {
  root: HTMLElement;
  apiBaseUrl: string;
  token: () => string | null;
  dock?: HudDock;
  fetchImpl?: typeof fetch;
  intervalMs?: number;
  onError?: (message: string) => void;
}

/**
 * Low-frequency DOM player directory. It is outside the Pixi render loop and
 * only reads the bounded nearby scope; it never receives private genos state.
 */
export class PlayerDirectoryPanel {
  private readonly root: HTMLElement;
  private readonly list: HTMLElement;
  private readonly timer: ReturnType<typeof setInterval>;
  private readonly options: PlayerDirectoryOptions;
  private disposed = false;

  constructor(options: PlayerDirectoryOptions) {
    this.options = options;
    this.root = document.createElement("aside");
    this.root.className = "social-directory";
    this.root.setAttribute("aria-label", "Nearby players");

    const header = document.createElement("div");
    header.className = "social-directory-header";
    header.textContent = "Nearby players";
    this.root.appendChild(header);

    this.list = document.createElement("ul");
    this.list.className = "social-directory-list";
    this.root.appendChild(this.list);
    if (options.dock) {
      options.dock.addPanel({
        id: "nearby",
        label: "Nearby",
        icon: "users",
        order: 10,
        content: this.root,
        onOpen: () => void this.refresh()
      });
    } else {
      options.root.appendChild(this.root);
    }

    this.timer = setInterval(() => void this.refresh(), options.intervalMs ?? 5000);
    void this.refresh();
  }

  async refresh(): Promise<void> {
    const token = this.options.token();
    if (token === null || this.disposed) {
      this.render([]);
      return;
    }

    try {
      const fetcher = this.options.fetchImpl ?? fetch;
      const response = await fetcher(`${this.options.apiBaseUrl}/api/social/presence?scope=nearby&radius=12`, {
        headers: { Authorization: `Bearer ${token}` }
      });
      if (!response.ok) {
        this.render([]);
        return;
      }
      const body = (await response.json()) as DirectoryResponse;
      this.render(Array.isArray(body.items) ? body.items : []);
    } catch (error) {
      this.options.onError?.(error instanceof Error ? error.message : "Presence unavailable");
      this.render([]);
    }
  }

  dispose(): void {
    this.disposed = true;
    clearInterval(this.timer);
    this.root.remove();
  }

  private render(items: readonly DirectoryPresence[]): void {
    if (this.disposed) return;
    this.list.replaceChildren();
    if (items.length === 0) {
      const empty = document.createElement("li");
      empty.className = "social-directory-empty";
      empty.textContent = "No players nearby";
      this.list.appendChild(empty);
      return;
    }

    for (const item of items) {
      const row = document.createElement("li");
      row.className = "social-directory-row";
      const name = document.createElement("span");
      name.className = "social-directory-name";
      name.textContent = item.displayName ?? "Resident";
      const meta = document.createElement("span");
      meta.className = "social-directory-meta";
      meta.textContent = `${item.state} · ${item.tileX},${item.tileY}`;
      row.append(name, meta);
      this.list.appendChild(row);
    }
  }
}
