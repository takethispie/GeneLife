import { describe, expect, it } from "vitest";
import { Container, Text, Texture } from "pixi.js";
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

function genos(networkId: number, x: number, y: number, isPlayerControlled = false): RenderGenos {
  return {
    networkId,
    x,
    y,
    tileX: Math.round(x),
    tileY: Math.round(y),
    appearance,
    isPlayerControlled,
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
function camera(): Camera {
  const value = new Camera();
  value.resize(1280, 720);
  value.setZoom(32);
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
});
