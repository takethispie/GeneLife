import { describe, expect, it } from "vitest";
import { DEFAULT_BINDINGS } from "../ui/settings";
import { InputMap, isTypingTarget } from "./input-map";

describe("InputMap", () => {
  it("matches configured keys case-insensitively", () => {
    const map = new InputMap(DEFAULT_BINDINGS);
    expect(map.matches({ key: "W" }, "panUp")).toBe(true);
    expect(map.matches({ key: "ArrowUp" }, "panUp")).toBe(true);
    expect(map.matches({ key: "x" }, "panUp")).toBe(false);
  });

  it("resolves an action for a key", () => {
    const map = new InputMap(DEFAULT_BINDINGS);
    expect(map.actionFor({ key: "Escape" })).toBe("cancel");
    expect(map.actionFor({ key: "F3" })).toBe("toggleDebug");
    expect(map.actionFor({ key: "q" })).toBeNull();
  });

  it("updates bindings and describes them", () => {
    const map = new InputMap(DEFAULT_BINDINGS);
    map.update({ ...DEFAULT_BINDINGS, panUp: ["i"] });
    expect(map.matches({ key: "i" }, "panUp")).toBe(true);
    expect(map.matches({ key: "w" }, "panUp")).toBe(false);
    expect(map.describe("panUp")).toBe("i");
  });

  it("detects typing targets", () => {
    const input = document.createElement("input");
    const textarea = document.createElement("textarea");
    const div = document.createElement("div");
    const hudControl = document.createElement("button");
    hudControl.dataset.hudControl = "true";
    expect(isTypingTarget(input)).toBe(true);
    expect(isTypingTarget(textarea)).toBe(true);
    expect(isTypingTarget(div)).toBe(false);
    expect(isTypingTarget(hudControl)).toBe(true);
  });
});
