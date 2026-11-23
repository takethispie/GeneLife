import { describe, expect, it } from "vitest";
import {
  bindingMatches,
  DEFAULT_BINDINGS,
  DEFAULT_SETTINGS,
  mergeSettings,
  SettingsStore
} from "./settings";

class MemoryStorage implements Storage {
  private values = new Map<string, string>();

  get length(): number {
    return this.values.size;
  }

  clear(): void {
    this.values.clear();
  }

  getItem(key: string): string | null {
    return this.values.get(key) ?? null;
  }

  key(index: number): string | null {
    return [...this.values.keys()][index] ?? null;
  }

  removeItem(key: string): void {
    this.values.delete(key);
  }

  setItem(key: string, value: string): void {
    this.values.set(key, value);
  }
}

describe("settings", () => {
  it("merges defaults and partial bindings", () => {
    const merged = mergeSettings({ reduceMotion: true, bindings: { ...DEFAULT_BINDINGS, panUp: ["i"] } });
    expect(merged.reduceMotion).toBe(true);
    expect(merged.bindings.panUp).toEqual(["i"]);
    expect(merged.bindings.panDown).toEqual(DEFAULT_BINDINGS.panDown);
  });

  it("persists, loads and resets", () => {
    const storage = new MemoryStorage();
    const store = new SettingsStore(storage);
    store.update({ hudScale: 1.4, debugOverlay: true });
    store.setBinding("panLeft", ["J"]);

    const restored = new SettingsStore(storage);
    restored.load();
    expect(restored.settings.hudScale).toBe(1.4);
    expect(restored.settings.debugOverlay).toBe(true);
    expect(restored.settings.bindings.panLeft).toEqual(["j"]);

    restored.reset();
    expect(restored.settings).toEqual(DEFAULT_SETTINGS);
  });

  it("falls back to defaults on corrupt storage", () => {
    const storage = new MemoryStorage();
    storage.setItem("genelife.settings", "{not json");
    const store = new SettingsStore(storage);
    expect(store.load()).toEqual(DEFAULT_SETTINGS);
  });

  it("matches bindings case-insensitively", () => {
    expect(bindingMatches({ key: "W" }, ["w"])).toBe(true);
    expect(bindingMatches({ key: "ArrowUp" }, ["arrowup"])).toBe(true);
    expect(bindingMatches({ key: "x" }, ["w"])).toBe(false);
  });
});
