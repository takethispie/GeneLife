import { describe, expect, it } from "vitest";
import { Container, Sprite, Text, Texture } from "pixi.js";
import type { AppearanceStatus } from "../contracts/hub";
import type { RenderGenos } from "../simulation/interpolation";
import type { RenderState } from "../simulation/store";
import { Camera } from "./camera";
import { GenosLayer } from "./genos-layer";
import type { SceneTextures } from "./textures";

const appearance: AppearanceStatus = {
  skinTone: 2,
  faceShape: 0,
  hairColor: 1,
  hairStyle: 2,
  eyeColor: 1,
  bodyFrame: 1,
  heightScale: 88,
  ageBand: 2,
  expression: 0,
  appearanceVersion: 1
};

function textures(): SceneTextures {
  return {
    white: Texture.WHITE,
    circle: Texture.WHITE,
    ring: Texture.WHITE,
    genos: {
      body: Texture.WHITE,
      head: Texture.WHITE,
      hair: Texture.WHITE,
      eye: Texture.WHITE,
      marker: Texture.WHITE
    }
  };
}

function genos(
  networkId: number,
  x: number,
  y: number,
  isPlayerControlled = false,
  isDead = false
): RenderGenos {
  return {
    networkId,
    x,
    y,
    tileX: Math.round(x),
    tileY: Math.round(y),
    appearance,
    isPlayerControlled,
    isDead,
    interpolated: false,
    snapped: false
  };
}

function state(genoses: RenderGenos[]): RenderState {
  return {
    tick: 1,
    targetTick: 1,
    genoses: new Map(genoses.map((g) => [g.networkId, g])),
    frameCount: 1,
    interpolationDelayMs: 0
  };
}

/** Screen is 1280x720 at zoom 32, so the viewport spans x ±20 and y ±11.25. */
function camera(zoom = 32): Camera {
  const value = new Camera();
  value.resize(1280, 720);
  value.setZoom(zoom);
  return value;
}

function namesOf(entries: Array<[number, string]>): (networkId: number) => string {
  const names = new Map(entries);
  return (networkId) => names.get(networkId) ?? "";
}

function texts(container: Container): Text[] {
  return container.children.filter((child): child is Text => child instanceof Text);
}

function drawnNames(container: Container): string[] {
  return texts(container)
    .map((label) => label.text)
    .filter((text) => text !== "");
}

/** Labels actually drawn this frame: visible and carrying text. */
function drawnLabels(container: Container): Text[] {
  return texts(container).filter((label) => label.visible && label.text !== "");
}

describe("GenosLayer name labels", () => {
  it("draws one label per visible genos and none for off-screen genoses", () => {
    const container = new Container();
    const layer = new GenosLayer(container, textures());

    const stats = layer.render(
      state([genos(1, 0, 0), genos(2, 3, 2), genos(3, -5, -3), genos(4, 500, 500)]),
      1,
      namesOf([
        [1, "Ada"],
        [2, "Boris"],
        [3, "Cleo"],
        [4, "Ghost"]
      ]),
      camera(),
      32
    );

    expect(stats.visible).toBe(3);
    expect(stats.labels).toBe(3);
    expect(new Set(drawnNames(container))).toEqual(new Set(["Ada", "Boris", "Cleo"]));
    expect(drawnNames(container)).not.toContain("Ghost");
  });

  it("hides every label when no genos is in the viewport", () => {
    const container = new Container();
    const layer = new GenosLayer(container, textures());

    const stats = layer.render(
      state([genos(9, 500, 500)]),
      1,
      namesOf([[9, "Far"]]),
      camera(),
      32
    );

    expect(stats.visible).toBe(0);
    expect(stats.labels).toBe(0);
    expect(drawnNames(container)).toEqual([]);
  });

  it("keeps the local label distinct and falls back to 'You' for an unnamed genos", () => {
    const container = new Container();
    const layer = new GenosLayer(container, textures());

    const stats = layer.render(state([genos(1, 0, 0), genos(2, 3, 2)]), 1, namesOf([[2, "Boris"]]), camera(), 32);

    expect(stats.localVisible).toBe(true);
    expect(stats.labels).toBe(2);

    const local = texts(container).find((label) => label.text === "You");
    const other = texts(container).find((label) => label.text === "Boris");
    expect(local).toBeDefined();
    expect(other).toBeDefined();
    expect(local).not.toBe(other);
    expect(local?.style.fill).not.toBe(other?.style.fill);
  });

  it("reuses pooled labels across frames instead of allocating one per genos", () => {
    const container = new Container();
    const layer = new GenosLayer(container, textures());
    const genoses = [genos(1, 0, 0), genos(2, 3, 2), genos(3, -5, -3)];
    const nameOf = namesOf([
      [1, "Ada"],
      [2, "Boris"],
      [3, "Cleo"]
    ]);
    const cam = camera();

    layer.render(state(genoses), 1, nameOf, cam, 32);
    const firstPass = texts(container);
    const countAfterFirst = firstPass.length;

    layer.render(state(genoses), 1, nameOf, cam, 32);
    const secondPass = texts(container);

    expect(secondPass.length).toBe(countAfterFirst);
    expect(secondPass.every((label, index) => label === firstPass[index])).toBe(true);
  });

  it("labels a dead genos with nothing while living genoses stay labelled", () => {
    const container = new Container();
    const layer = new GenosLayer(container, textures());

    const stats = layer.render(
      state([genos(1, 0, 0, false, true), genos(2, 3, 2), genos(3, -5, -3)]),
      99,
      namesOf([
        [1, "Ada"],
        [2, "Boris"],
        [3, "Cleo"]
      ]),
      camera(),
      32
    );

    expect(stats.visible).toBe(3);
    expect(stats.labels).toBe(2);
    expect(new Set(drawnNames(container))).toEqual(new Set(["Boris", "Cleo"]));
    expect(drawnNames(container)).not.toContain("Ada");
  });

  it("suppresses the local genos' own nameplate when it dies but keeps its ring", () => {
    const container = new Container();
    const layer = new GenosLayer(container, textures());

    const stats = layer.render(
      state([genos(1, 0, 0, true, true), genos(2, 3, 2)]),
      1,
      namesOf([
        [1, "Ada"],
        [2, "Boris"]
      ]),
      camera(),
      32
    );

    expect(stats.localVisible).toBe(true);
    expect(stats.labels).toBe(1);
    expect(drawnNames(container)).toEqual(["Boris"]);

    const ring = container.children.find((child) => child instanceof Sprite);
    expect(ring?.visible).toBe(true);
  });

  it("fades nameplates out as the camera zooms far out", () => {
    const genoses = [genos(2, 0, 0), genos(3, 3, 2)];
    const nameOf = namesOf([
      [2, "Boris"],
      [3, "Cleo"]
    ]);

    const nearContainer = new Container();
    const near = new GenosLayer(nearContainer, textures());
    const nearStats = near.render(state(genoses), 99, nameOf, camera(32), 32);
    expect(nearStats.labels).toBe(2);
    const nearLabels = drawnLabels(nearContainer);
    expect(nearLabels).toHaveLength(2);
    expect(nearLabels.every((label) => label.alpha === 1)).toBe(true);

    const midContainer = new Container();
    const mid = new GenosLayer(midContainer, textures());
    const midStats = mid.render(state(genoses), 99, nameOf, camera(19), 32);
    expect(midStats.labels).toBe(2);
    const midLabels = drawnLabels(midContainer);
    expect(midLabels).toHaveLength(2);
    for (const label of midLabels) {
      expect(label.alpha).toBeGreaterThan(0);
      expect(label.alpha).toBeLessThan(1);
    }

    const farContainer = new Container();
    const far = new GenosLayer(farContainer, textures());
    const farStats = far.render(state(genoses), 99, nameOf, camera(12), 32);
    expect(farStats.labels).toBe(0);
    expect(drawnLabels(farContainer)).toEqual([]);
  });
});
