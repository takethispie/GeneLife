export type LoadingStep = "config" | "auth" | "assets" | "scene";
export type StepStatus = "pending" | "active" | "done" | "failed";

/** Explicit loading screen with the four bootstrap steps. */
export class LoadingScreen {
  private readonly element: HTMLElement;
  private readonly steps = new Map<LoadingStep, HTMLElement>();
  private readonly errorElement: HTMLElement;
  private readonly hintElement: HTMLElement;
  private readonly retryButton: HTMLButtonElement;
  private hidden = false;

  constructor(parent: HTMLElement) {
    this.element = document.createElement("div");
    this.element.className = "loading-overlay";
    this.element.id = "loading";

    const card = document.createElement("div");
    card.className = "loading-card";
    const title = document.createElement("h1");
    title.textContent = "GeneLife";
    const subtitle = document.createElement("p");
    subtitle.textContent = "Loading the browser client…";
    const list = document.createElement("ul");
    list.className = "loading-steps";

    for (const step of ["config", "auth", "assets", "scene"] as LoadingStep[]) {
      const item = document.createElement("li");
      item.dataset.step = step;
      item.dataset.status = "pending";
      item.textContent = STEP_LABELS[step];
      this.steps.set(step, item);
      list.appendChild(item);
    }

    this.errorElement = document.createElement("p");
    this.errorElement.className = "loading-error";
    this.hintElement = document.createElement("p");
    this.hintElement.className = "loading-hint";
    this.retryButton = document.createElement("button");
    this.retryButton.type = "button";
    this.retryButton.textContent = "Retry";
    this.retryButton.hidden = true;

    card.append(title, subtitle, list, this.errorElement, this.hintElement, this.retryButton);
    this.element.appendChild(card);
    parent.appendChild(this.element);
  }

  setStep(step: LoadingStep, status: StepStatus, detail?: string): void {
    const item = this.steps.get(step);
    if (item === undefined) return;
    item.dataset.status = status;
    item.textContent = detail === undefined ? STEP_LABELS[step] : `${STEP_LABELS[step]} — ${detail}`;
  }

  setHint(text: string): void {
    this.hintElement.textContent = text;
  }

  setRetry(callback: () => void): void {
    this.retryButton.hidden = false;
    this.retryButton.onclick = callback;
  }

  fail(message: string): void {
    this.errorElement.textContent = message;
    this.element.hidden = false;
    this.hidden = false;
    this.retryButton.hidden = false;
    for (const item of this.steps.values()) {
      if (item.dataset.status === "active") item.dataset.status = "failed";
    }
  }

  hide(): void {
    this.hidden = true;
    this.element.hidden = true;
  }

  get isHidden(): boolean {
    return this.hidden;
  }

  destroy(): void {
    this.element.remove();
  }
}

const STEP_LABELS: Record<LoadingStep, string> = {
  config: "Configuration",
  auth: "Authentication",
  assets: "Assets",
  scene: "World scene"
};
