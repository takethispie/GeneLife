export type KeyBindingAction =
  | "panUp"
  | "panDown"
  | "panLeft"
  | "panRight"
  | "zoomIn"
  | "zoomOut"
  | "cancel"
  | "toggleDebug";

export type KeyBindings = Record<KeyBindingAction, string[]>;

export type AudioCategory = "ui" | "notification" | "chat" | "life" | "ambient";
export type Handedness = "right" | "left";

export interface ClientSettings {
  reduceMotion: boolean;
  hudScale: number;
  debugOverlay: boolean;
  soundVolume: number;
  audioMuted: boolean;
  audioCategories: Record<AudioCategory, boolean>;
  touchControls: boolean;
  handedness: Handedness;
  bindings: KeyBindings;
}

export const DEFAULT_BINDINGS: KeyBindings = {
  panUp: ["w", "arrowup"],
  panDown: ["s", "arrowdown"],
  panLeft: ["a", "arrowleft"],
  panRight: ["d", "arrowright"],
  zoomIn: ["+", "="],
  zoomOut: ["-"],
  cancel: ["escape"],
  toggleDebug: ["f3"]
};

export const DEFAULT_AUDIO_CATEGORIES: Record<AudioCategory, boolean> = {
  ui: true,
  notification: true,
  chat: true,
  life: true,
  ambient: true
};

export const DEFAULT_SETTINGS: ClientSettings = {
  reduceMotion: false,
  hudScale: 1,
  debugOverlay: false,
  soundVolume: 0,
  audioMuted: false,
  audioCategories: DEFAULT_AUDIO_CATEGORIES,
  touchControls: true,
  handedness: "right",
  bindings: DEFAULT_BINDINGS
};

const STORAGE_KEY = "genelife.settings";

export class SettingsStore {
  private current: ClientSettings;
  private readonly listeners = new Set<(settings: ClientSettings) => void>();

  constructor(
    private readonly storage: Storage | null = safeStorage(),
    initial?: Partial<ClientSettings>
  ) {
    this.current = mergeSettings(initial);
  }

  get settings(): ClientSettings {
    return this.current;
  }

  load(): ClientSettings {
    if (this.storage !== null) {
      try {
        const raw = this.storage.getItem(STORAGE_KEY);
        if (raw !== null) this.current = mergeSettings(JSON.parse(raw) as Partial<ClientSettings>);
      } catch {
        this.current = mergeSettings();
      }
    }
    this.emit();
    return this.current;
  }

  update(partial: Partial<ClientSettings>): void {
    this.current = mergeSettings({ ...this.current, ...partial });
    this.persist();
    this.emit();
  }

  setAudioCategory(category: AudioCategory, enabled: boolean): void {
    this.update({
      audioCategories: { ...this.current.audioCategories, [category]: enabled }
    });
  }

  setBinding(action: KeyBindingAction, keys: string[]): void {
    this.update({
      bindings: { ...this.current.bindings, [action]: keys.map((key) => key.toLowerCase()) }
    });
  }

  reset(): void {
    this.current = mergeSettings();
    this.persist();
    this.emit();
  }

  subscribe(listener: (settings: ClientSettings) => void): () => void {
    this.listeners.add(listener);
    listener(this.current);
    return () => this.listeners.delete(listener);
  }

  applyToDocument(): void {
    if (typeof document === "undefined") return;
    document.documentElement.style.setProperty("--hud-scale", String(this.current.hudScale));
    document.documentElement.classList.toggle("reduce-motion", this.current.reduceMotion);
    document.documentElement.classList.toggle("show-debug", this.current.debugOverlay);
    document.documentElement.classList.toggle("touch-mode", this.current.touchControls);
    document.documentElement.classList.toggle("left-handed", this.current.handedness === "left");
  }

  prefersReducedMotion(): boolean {
    if (this.current.reduceMotion) return true;
    return (
      typeof window !== "undefined" &&
      typeof window.matchMedia === "function" &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches
    );
  }

  private persist(): void {
    if (this.storage === null) return;
    try {
      this.storage.setItem(STORAGE_KEY, JSON.stringify(this.current));
    } catch {
      // Quota or privacy mode: keep in-memory settings.
    }
  }

  private emit(): void {
    for (const listener of this.listeners) listener(this.current);
  }
}

export function mergeSettings(partial?: Partial<ClientSettings>): ClientSettings {
  return {
    ...DEFAULT_SETTINGS,
    ...partial,
    bindings: {
      ...DEFAULT_BINDINGS,
      ...(partial?.bindings ?? {})
    },
    audioCategories: {
      ...DEFAULT_AUDIO_CATEGORIES,
      ...(partial?.audioCategories ?? {})
    }
  };
}

export function eventKey(event: Pick<KeyboardEvent, "key">): string {
  return event.key.toLowerCase();
}

export function bindingMatches(event: Pick<KeyboardEvent, "key">, keys: readonly string[]): boolean {
  return keys.includes(eventKey(event));
}

function safeStorage(): Storage | null {
  try {
    return typeof localStorage === "undefined" ? null : localStorage;
  } catch {
    return null;
  }
}
