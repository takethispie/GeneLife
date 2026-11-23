import { Graphics, Texture, type Renderer } from "pixi.js";
import { createAppearanceTextures, type AppearanceTextures } from "./appearance/sprite-pool";

export interface SceneTextures {
  white: Texture;
  circle: Texture;
  ring: Texture;
  sim: AppearanceTextures;
}

/**
 * Generates the small procedural texture set. Missing generation falls back to
 * Pixi's white texture, so a texture failure is visible but never crashes the
 * scene. Appearance parts are shared regions tinted from palettes.
 */
export function createSceneTextures(renderer: Renderer): SceneTextures {
  return {
    white: Texture.WHITE,
    circle: generate(renderer, (graphics) => graphics.circle(8, 8, 8).fill(0xffffff)),
    ring: generate(renderer, (graphics) =>
      graphics.circle(10, 10, 10).stroke({ width: 3, color: 0xffffff })
    ),
    sim: createAppearanceTextures(renderer)
  };
}

export function destroyTextures(textures: SceneTextures): void {
  for (const texture of [textures.circle, textures.ring]) {
    if (texture !== Texture.WHITE) texture.destroy(true);
  }
  for (const texture of Object.values(textures.sim)) {
    if (texture !== Texture.WHITE) texture.destroy(true);
  }
}

function generate(renderer: Renderer, draw: (graphics: Graphics) => void): Texture {
  try {
    const graphics = new Graphics();
    draw(graphics);
    const texture = renderer.generateTexture(graphics);
    graphics.destroy();
    return texture;
  } catch {
    return Texture.WHITE;
  }
}
