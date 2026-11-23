import { describe, expect, it } from "vitest";
import { NotificationCenter } from "./notifications";

describe("NotificationCenter", () => {
  it("pushes, dedupes and emits", () => {
    let now = 1_000;
    const center = new NotificationCenter({ now: () => now, dedupeWindowMs: 4_000 });
    const seen: number[] = [];
    center.subscribe((items) => seen.push(items.length));

    expect(center.push("info", "hello", "Hello")).not.toBeNull();
    expect(center.push("info", "hello", "Hello")).toBeNull();
    now += 4_001;
    expect(center.push("info", "hello", "Hello")).not.toBeNull();
    expect(seen).toEqual([0, 1, 2]);
  });

  it("keeps the newest entries within the limit", () => {
    const center = new NotificationCenter({ limit: 2 });
    center.push("info", "a", "A");
    center.push("info", "b", "B");
    center.push("info", "c", "C");
    expect(center.list.map((item) => item.code)).toEqual(["b", "c"]);
  });

  it("dismisses by id and clears", () => {
    const center = new NotificationCenter();
    const first = center.push("info", "a", "A");
    center.push("info", "b", "B");
    if (first === null) throw new Error("expected notification");
    center.dismiss(first.id);
    expect(center.list.map((item) => item.code)).toEqual(["b"]);
    center.clear();
    expect(center.list).toHaveLength(0);
  });
});
