import { describe, expect, it, vi } from "vitest";
import { AudioCuePlayer, AUDIO_CUES } from "./audio";
import { mergeSettings } from "../settings";

function fakeContext() {
  const oscillators: Array<{
    type: string;
    frequency: FakeParam;
    start: ReturnType<typeof vi.fn>;
    stop: ReturnType<typeof vi.fn>;
    connect: ReturnType<typeof vi.fn>;
    disconnect: ReturnType<typeof vi.fn>;
  }> = [];
  const gains: Array<{
    gain: FakeParam;
    connect: ReturnType<typeof vi.fn>;
    disconnect: ReturnType<typeof vi.fn>;
  }> = [];

  class FakeParam {
    values: number[] = [];
    setValueAtTime = vi.fn((value: number) => {
      this.values.push(value);
      return this;
    });
    linearRampToValueAtTime = vi.fn(() => this);
    exponentialRampToValueAtTime = vi.fn(() => this);
  }

  const context = {
    currentTime: 0,
    state: "running",
    destination: {},
    resume: vi.fn(),
    close: vi.fn(),
    createOscillator: vi.fn(() => {
      const oscillator = {
        type: "sine",
        frequency: new FakeParam(),
        start: vi.fn(),
        stop: vi.fn(),
        connect: vi.fn(),
        disconnect: vi.fn()
      };
      oscillators.push(oscillator);
      return oscillator;
    }),
    createGain: vi.fn(() => {
      const gain = { gain: new FakeParam(), connect: vi.fn(), disconnect: vi.fn() };
      gains.push(gain);
      return gain;
    })
  };

  return { context: context as unknown as AudioContext, oscillators, gains };
}

describe("audio cues", () => {
  it("does nothing before a user gesture creates the context", () => {
    const fake = fakeContext();
    const player = new AudioCuePlayer({
      getSettings: () => mergeSettings({ soundVolume: 0.6 }),
      createContext: () => fake.context
    });

    expect(player.play("uiConfirm")).toBe(false);
    expect(player.hasContext).toBe(false);
    expect(fake.oscillators).toHaveLength(0);
  });

  it("plays a cue after unlock and respects mute and category settings", () => {
    const fake = fakeContext();
    let settings = mergeSettings({
      soundVolume: 0.6,
      audioCategories: { ...mergeSettings().audioCategories, ambient: false }
    });
    const player = new AudioCuePlayer({
      getSettings: () => settings,
      createContext: () => fake.context
    });

    expect(player.unlock()).toBe(true);
    expect(player.play("uiConfirm")).toBe(true);
    expect(fake.oscillators).toHaveLength(1);
    expect(fake.oscillators[0]?.type).toBe(AUDIO_CUES.uiConfirm.wave);

    settings = mergeSettings({ ...settings, audioMuted: true });
    expect(player.play("uiConfirm")).toBe(false);

    settings = mergeSettings({
      ...settings,
      audioMuted: false,
      audioCategories: { ...settings.audioCategories, ui: false }
    });
    expect(player.play("uiConfirm")).toBe(false);
    expect(fake.oscillators).toHaveLength(1);
  });

  it("keeps audio off when volume is zero even after unlock", () => {
    const fake = fakeContext();
    const player = new AudioCuePlayer({
      getSettings: () =>
        mergeSettings({
          soundVolume: 0,
          audioCategories: { ...mergeSettings().audioCategories, ambient: false }
        }),
      createContext: () => fake.context
    });

    player.unlock();
    expect(player.play("notification")).toBe(false);
    expect(fake.oscillators).toHaveLength(0);
  });

  it("starts and stops the ambient loop with settings changes", () => {
    const fake = fakeContext();
    let settings = mergeSettings({ soundVolume: 0.4 });
    const player = new AudioCuePlayer({
      getSettings: () => settings,
      createContext: () => fake.context
    });

    player.unlock();
    expect(fake.oscillators).toHaveLength(1);
    expect(fake.oscillators[0]?.start).toHaveBeenCalled();

    settings = mergeSettings({ ...settings, audioCategories: { ...settings.audioCategories, ambient: false } });
    player.applySettings();
    expect(fake.oscillators[0]?.stop).toHaveBeenCalled();

    player.dispose();
    expect(fake.context.close).toHaveBeenCalled();
  });
});
