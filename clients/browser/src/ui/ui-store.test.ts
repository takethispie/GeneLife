import { describe, expect, it } from "vitest";
import { INITIAL_HUD_STATE, UiStore } from "./ui-store";

describe("UiStore", () => {
  it("notifies only when a value changes", () => {
    const store = new UiStore();
    const patches: number[] = [];
    store.subscribe((state) => patches.push(state.fps));
    store.patch({ fps: 60 });
    store.patch({ fps: 60 });
    store.patch({ fps: 59 });
    expect(patches).toEqual([0, 60, 59]);
  });

  it("preserves unrelated fields", () => {
    const store = new UiStore();
    store.patch({ playerId: "p", tick: 10 });
    expect(store.getState()).toMatchObject({ playerId: "p", tick: 10, connection: "idle" });
  });

  it("unsubscribes", () => {
    const store = new UiStore();
    let count = 0;
    const unsubscribe = store.subscribe(() => count++);
    unsubscribe();
    store.patch({ tick: 1 });
    expect(count).toBe(1);
  });

  it("resets to initial state", () => {
    const store = new UiStore();
    store.patch({ tick: 99, selection: { kind: "tile", x: 1, y: 2 } });
    store.reset();
    expect(store.getState()).toEqual(INITIAL_HUD_STATE);
  });
});
