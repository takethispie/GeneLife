export interface RespawnPopinResult {
  ok: boolean;
  message?: string;
}

export interface RespawnPopinInfo {
  name: string;
  age?: number;
  cause?: string | null;
}

export interface RespawnPopinOptions {
  root: HTMLElement;
  onRespawn: () => Promise<RespawnPopinResult>;
}

/**
 * Blocking death prompt. It stays open until the respawn call succeeds so a
 * dropped request cannot strand the player without a controlled sim.
 */
export class RespawnPopin {
  private readonly overlay: HTMLElement;
  private readonly detail: HTMLElement;
  private readonly error: HTMLElement;
  private readonly button: HTMLButtonElement;
  private busy = false;

  constructor(private readonly options: RespawnPopinOptions) {
    this.overlay = document.createElement("div");
    this.overlay.className = "respawn-popin";
    this.overlay.dataset.hudControl = "true";
    this.overlay.setAttribute("role", "dialog");
    this.overlay.setAttribute("aria-modal", "true");
    this.overlay.setAttribute("aria-labelledby", "respawn-title");
    this.overlay.hidden = true;

    const card = document.createElement("div");
    card.className = "respawn-card";

    const eyebrow = document.createElement("p");
    eyebrow.className = "respawn-eyebrow";
    eyebrow.textContent = "Life complete";

    const title = document.createElement("h2");
    title.id = "respawn-title";
    title.className = "respawn-title";
    title.textContent = "Your sim has died";

    this.detail = document.createElement("p");
    this.detail.className = "respawn-detail";

    this.error = document.createElement("p");
    this.error.className = "respawn-error";
    this.error.setAttribute("role", "alert");
    this.error.hidden = true;

    this.button = document.createElement("button");
    this.button.type = "button";
    this.button.className = "respawn-button";
    this.button.textContent = "Respawn as child";
    this.button.addEventListener("click", () => {
      void this.submit();
    });

    const hint = document.createElement("p");
    hint.className = "respawn-hint";
    hint.textContent = "Continue the family as their grown child.";

    card.append(eyebrow, title, this.detail, this.error, this.button, hint);
    this.overlay.append(card);
    this.options.root.append(this.overlay);
  }

  get isOpen(): boolean {
    return !this.overlay.hidden;
  }

  show(info: RespawnPopinInfo): void {
    const age = typeof info.age === "number" && info.age > 0 ? ` at age ${info.age}` : "";
    this.detail.textContent = `${info.name} died${age}. Their child can carry the family forward.`;
    this.error.hidden = true;
    this.error.textContent = "";
    this.overlay.hidden = false;
    this.button.focus();
  }

  hide(): void {
    this.overlay.hidden = true;
  }

  dispose(): void {
    this.overlay.remove();
  }

  private async submit(): Promise<void> {
    if (this.busy) return;
    this.busy = true;
    this.button.disabled = true;
    this.button.textContent = "Respawning...";
    this.error.hidden = true;

    try {
      const result = await this.options.onRespawn();
      if (result.ok) {
        this.hide();
        return;
      }
      this.error.textContent = result.message ?? "Could not respawn.";
      this.error.hidden = false;
    } catch (error) {
      this.error.textContent =
        error instanceof Error ? error.message : "Could not respawn.";
      this.error.hidden = false;
    } finally {
      this.busy = false;
      this.button.disabled = false;
      this.button.textContent = "Respawn as child";
    }
  }
}
