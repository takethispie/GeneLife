import { describe, expect, it, vi } from "vitest";
import { TouchGestureRecognizer } from "./touch";

function harness() {
  let clock = 0;
  const onPan = vi.fn();
  const onZoom = vi.fn();
  const recognizer = new TouchGestureRecognizer(
    { onPan, onZoom },
    { now: () => clock, tapSlopPixels: 10, maxTapMs: 300 }
  );
  return {
    recognizer,
    onPan,
    onZoom,
    tick: (ms: number) => {
      clock += ms;
    }
  };
}

describe("touch gestures", () => {
  it("treats a quick, still press as a tap and leaves the click path intact", () => {
    const h = harness();
    h.recognizer.down(1, 40, 40);
    h.tick(120);
    expect(h.recognizer.up(1)).toBe(false);
    expect(h.onPan).not.toHaveBeenCalled();
    expect(h.onZoom).not.toHaveBeenCalled();
  });

  it("pans on a one-finger drag and suppresses the synthesized click", () => {
    const h = harness();
    h.recognizer.down(1, 0, 0);
    h.tick(50);
    h.recognizer.move(1, 30, 10);
    h.tick(100);
    expect(h.recognizer.up(1)).toBe(true);
    expect(h.onPan).toHaveBeenLastCalledWith(30, 10);
  });

  it("zooms on a two-finger pinch around the pointer midpoint", () => {
    const h = harness();
    h.recognizer.down(1, 0, 0);
    h.recognizer.down(2, 100, 0);
    h.recognizer.move(2, 200, 0);

    expect(h.onZoom).toHaveBeenCalledWith(2, 100, 0);
    expect(h.recognizer.up(1)).toBe(true);
    expect(h.recognizer.up(2)).toBe(true);
  });

  it("cancels cleanly when a touch is interrupted", () => {
    const h = harness();
    h.recognizer.down(1, 0, 0);
    h.recognizer.cancelAll();
    expect(h.recognizer.active).toBe(0);
    expect(h.recognizer.up(1)).toBe(false);
  });

  it("does not emit a gesture for a tap that ends after the tap window", () => {
    const h = harness();
    h.recognizer.down(1, 10, 10);
    h.tick(500);
    expect(h.recognizer.up(1)).toBe(false);
    expect(h.onPan).not.toHaveBeenCalled();
  });

  it("reports a tap through onTap with the pointer coordinates", () => {
    let clock = 0;
    const onTap = vi.fn();
    const recognizer = new TouchGestureRecognizer(
      { onPan: vi.fn(), onZoom: vi.fn(), onTap },
      { now: () => clock, tapSlopPixels: 10, maxTapMs: 300 }
    );
    recognizer.down(1, 42, 24);
    clock += 100;
    expect(recognizer.up(1)).toBe(false);
    expect(onTap).toHaveBeenCalledWith(42, 24);
  });

  it("does not report a drag as a tap", () => {
    let clock = 0;
    const onTap = vi.fn();
    const recognizer = new TouchGestureRecognizer(
      { onPan: vi.fn(), onZoom: vi.fn(), onTap },
      { now: () => clock, tapSlopPixels: 10, maxTapMs: 300 }
    );
    recognizer.down(1, 0, 0);
    clock += 50;
    recognizer.move(1, 30, 10);
    expect(recognizer.up(1)).toBe(true);
    expect(onTap).not.toHaveBeenCalled();
  });
});
