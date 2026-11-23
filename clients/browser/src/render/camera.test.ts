import { describe, expect, it } from "vitest";
import { validateMapSnapshot } from "../contracts/validate";
import { decodeMap } from "../simulation/map/rle-decoder";
import mapFixture from "../test/fixtures/map-snapshot.json";
import { Camera } from "./camera";

const map = decodeMap(validateMapSnapshot(mapFixture));

function camera(): Camera {
  const value = new Camera();
  value.resize(1280, 720);
  value.setMap(map);
  value.setZoom(64);
  value.centerOnTile(0, 0);
  return value;
}

describe("Camera", () => {
  it("maps the screen center to the center tile", () => {
    const c = camera();
    expect(c.tileFromScreen(640, 360)).toEqual({ x: 0, y: 0 });
    expect(c.containerX()).toBeCloseTo(640, 5);
  });

  it("round-trips world and tile coordinates", () => {
    const c = camera();
    c.centerOnTile(3, -4);
    const tile = c.tileFromScreen(640, 360);
    expect(tile).toEqual({ x: 3, y: -4 });
    const world = c.worldFromScreen(640, 360);
    expect(world).toEqual({ x: 3, y: -4 });
  });

  it("clamps panning to the map bounds", () => {
    const c = camera();
    c.panByTiles(10_000, 10_000);
    c.snapToTarget();
    const rect = c.visibleTileRect(map);
    expect(rect.maxX).toBeLessThanOrEqual(map.maxX);
    expect(rect.maxY).toBeLessThanOrEqual(map.maxY);

    c.panByTiles(-20_000, -20_000);
    c.snapToTarget();
    const rect2 = c.visibleTileRect(map);
    expect(rect2.minX).toBeGreaterThanOrEqual(map.minX);
    expect(rect2.minY).toBeGreaterThanOrEqual(map.minY);
  });

  it("keeps the world point under the pointer while zooming", () => {
    const c = camera();
    c.snapToTarget();
    const before = c.worldFromScreen(900, 200);
    c.zoomAt(1.5, 900, 200);
    c.snapToTarget();
    const after = c.worldFromScreen(900, 200);
    expect(after.x).toBeCloseTo(before.x, 5);
    expect(after.y).toBeCloseTo(before.y, 5);
  });

  it("clamps zoom to limits", () => {
    const c = camera();
    c.setZoom(10_000);
    expect(c.zoom).toBeLessThanOrEqual(128);
    c.setZoom(0.0001);
    expect(c.zoom).toBeGreaterThanOrEqual(0);
  });

  it("applies reduced motion immediately", () => {
    const c = new Camera({ reducedMotion: true });
    c.resize(1280, 720);
    c.setMap(map);
    c.setZoom(64);
    c.centerOnWorld(0, 0);
    expect(c.center).toEqual({ x: 0, y: 0 });
  });

  it("reports visible tiles inside bounds", () => {
    const c = camera();
    const rect = c.visibleTileRect(map);
    expect(rect.minX).toBeGreaterThanOrEqual(map.minX);
    expect(rect.maxX).toBeLessThanOrEqual(map.maxX);
    expect(rect.minY).toBeGreaterThanOrEqual(map.minY);
    expect(rect.maxY).toBeLessThanOrEqual(map.maxY);
    expect(rect.maxX).toBeGreaterThan(rect.minX);
  });
});
