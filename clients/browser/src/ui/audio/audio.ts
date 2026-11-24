import type { AudioCategory, ClientSettings } from "../settings";

export type AudioCue =
  | "uiConfirm"
  | "uiCancel"
  | "notification"
  | "chat"
  | "birth"
  | "death"
  | "utilityOutage";

export interface AudioCueDefinition {
  category: AudioCategory;
  frequency: number;
  endFrequency: number;
  durationMs: number;
  gain: number;
  wave: OscillatorType;
  visualLabel: string;
}

/**
 * A tiny synthesized cue set. No audio files are loaded and nothing runs in the
 * simulation tick. Every cue names the visual notification that must also be
 * shown, so audio is never the only signal.
 */
export const AUDIO_CUES: Record<AudioCue, AudioCueDefinition> = {
  uiConfirm: {
    category: "ui",
    frequency: 620,
    endFrequency: 760,
    durationMs: 90,
    gain: 0.18,
    wave: "triangle",
    visualLabel: "Action accepted notification"
  },
  uiCancel: {
    category: "ui",
    frequency: 240,
    endFrequency: 170,
    durationMs: 130,
    gain: 0.2,
    wave: "square",
    visualLabel: "Action rejected notification"
  },
  notification: {
    category: "notification",
    frequency: 520,
    endFrequency: 520,
    durationMs: 110,
    gain: 0.12,
    wave: "sine",
    visualLabel: "HUD notification"
  },
  chat: {
    category: "chat",
    frequency: 880,
    endFrequency: 920,
    durationMs: 70,
    gain: 0.1,
    wave: "sine",
    visualLabel: "Chat message in the chat log"
  },
  birth: {
    category: "life",
    frequency: 440,
    endFrequency: 880,
    durationMs: 220,
    gain: 0.14,
    wave: "triangle",
    visualLabel: "Family notification"
  },
  death: {
    category: "life",
    frequency: 320,
    endFrequency: 140,
    durationMs: 320,
    gain: 0.16,
    wave: "triangle",
    visualLabel: "Lifecycle notification"
  },
  utilityOutage: {
    category: "notification",
    frequency: 180,
    endFrequency: 120,
    durationMs: 260,
    gain: 0.18,
    wave: "sawtooth",
    visualLabel: "Consumption outage notification"
  }
};

export interface AudioContextFactory {
  (): AudioContext | null;
}

export interface AudioCuePlayerOptions {
  getSettings: () => ClientSettings;
  createContext?: AudioContextFactory;
  now?: () => number;
}

/**
 * Lazy WebAudio player. The context is created only after a user gesture, so the
 * browser autoplay policy is respected. Volume, mute and per-category settings
 * are read at play time; a muted cue is a no-op, never a queued sound.
 */
export class AudioCuePlayer {
  private readonly getSettings: () => ClientSettings;
  private readonly createContext: AudioContextFactory;
  private readonly now: () => number;
  private context: AudioContext | null = null;
  private unlocked = false;
  private ambient: { oscillator: OscillatorNode; gain: GainNode } | null = null;
  private disposed = false;

  constructor(options: AudioCuePlayerOptions) {
    this.getSettings = options.getSettings;
    this.createContext = options.createContext ?? defaultContextFactory;
    this.now = options.now ?? (() => Date.now());
  }

  get isUnlocked(): boolean {
    return this.unlocked;
  }

  get hasContext(): boolean {
    return this.context !== null;
  }

  /** Must be called from a real user gesture handler. */
  unlock(): boolean {
    if (this.disposed) return false;
    if (this.context === null) {
      this.context = this.createContext();
      if (this.context === null) return false;
    }
    this.unlocked = true;
    if (this.context.state === "suspended") void this.context.resume();
    this.applySettings();
    return true;
  }

  canPlay(cue: AudioCue): boolean {
    if (this.disposed || !this.unlocked || this.context === null) return false;
    const settings = this.getSettings();
    if (settings.audioMuted || settings.soundVolume <= 0) return false;
    return settings.audioCategories[AUDIO_CUES[cue].category];
  }

  play(cue: AudioCue): boolean {
    if (!this.canPlay(cue) || this.context === null) return false;
    const definition = AUDIO_CUES[cue];
    const context = this.context;
    const oscillator = context.createOscillator();
    const gain = context.createGain();
    const volume = Math.min(1, Math.max(0, this.getSettings().soundVolume));

    oscillator.type = definition.wave;
    oscillator.frequency.setValueAtTime(definition.frequency, context.currentTime);
    oscillator.frequency.linearRampToValueAtTime(
      definition.endFrequency,
      context.currentTime + definition.durationMs / 1000
    );
    gain.gain.setValueAtTime(definition.gain * volume, context.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.0001, context.currentTime + definition.durationMs / 1000);

    oscillator.connect(gain);
    gain.connect(context.destination);
    oscillator.start(context.currentTime);
    oscillator.stop(context.currentTime + definition.durationMs / 1000);
    return true;
  }

  /** Applies mute/volume/category changes to a running ambient loop. */
  applySettings(): void {
    if (this.disposed || !this.unlocked || this.context === null) return;
    const settings = this.getSettings();
    const wantsAmbient =
      !settings.audioMuted &&
      settings.soundVolume > 0 &&
      settings.audioCategories.ambient;

    if (!wantsAmbient) {
      this.stopAmbient();
      return;
    }

    if (this.ambient !== null) {
      this.ambient.gain.gain.setValueAtTime(
        Math.min(0.02, 0.02 * settings.soundVolume),
        this.context.currentTime
      );
      return;
    }

    const oscillator = this.context.createOscillator();
    const gain = this.context.createGain();
    oscillator.type = "sine";
    oscillator.frequency.setValueAtTime(110, this.context.currentTime);
    gain.gain.setValueAtTime(Math.min(0.02, 0.02 * settings.soundVolume), this.context.currentTime);
    oscillator.connect(gain);
    gain.connect(this.context.destination);
    oscillator.start(this.context.currentTime);
    this.ambient = { oscillator, gain };
  }

  stopAmbient(): void {
    if (this.ambient === null) return;
    try {
      this.ambient.oscillator.stop(this.context?.currentTime ?? this.now() / 1000);
      this.ambient.oscillator.disconnect();
      this.ambient.gain.disconnect();
    } catch {
      // Already stopped by the browser.
    }
    this.ambient = null;
  }

  dispose(): void {
    if (this.disposed) return;
    this.disposed = true;
    this.stopAmbient();
    const context = this.context;
    this.context = null;
    this.unlocked = false;
    if (context !== null && context.state !== "closed") void context.close();
  }
}

function defaultContextFactory(): AudioContext | null {
  if (typeof window === "undefined") return null;
  const ctor =
    (window as unknown as { AudioContext?: typeof AudioContext }).AudioContext ??
    (window as unknown as { webkitAudioContext?: typeof AudioContext }).webkitAudioContext;
  return ctor === undefined ? null : new ctor();
}
