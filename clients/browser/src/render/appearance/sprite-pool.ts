import { Container, Graphics, Sprite, Texture, type Renderer } from "pixi.js";
import { DisplayPool } from "../pools";
import { appearanceCacheKey, composeAppearance, isKnownPart, type AppearanceParts } from "./parts";

export interface AppearanceTextures {
  body: Texture;
  head: Texture;
  hair: Texture;
  eye: Texture;
  marker: Texture;
}

const SKIN_COLORS: Record<string, number> = {
  porcelain: 0xf3d8c5, fair: 0xe8c3a5, olive: 0xcba37a,
  tan: 0xb07a4f, brown: 0x8a5a35, deep: 0x5c3a24, neutral: 0xcba37a
};
const HAIR_COLORS: Record<string, number> = {
  black: 0x1f1a17, brown: 0x5b3a24, blonde: 0xd8b45f, ginger: 0xb5522c, grey: 0xc9c9c9
};
const EYE_COLORS: Record<string, number> = {
  brown: 0x6b4226, blue: 0x4b83b5, green: 0x4c7a4a, grey: 0x8a8f96, hazel: 0xa68a4c
};
const OUTFIT_COLORS: Record<string, number> = {
  "outfit-child": 0xf2a65a, "outfit-student": 0x6f9bd1, "outfit-worker": 0x5a7d6b,
  "outfit-senior": 0x9a8fb8, "outfit-player": 0x67e8f9, "outfit-fallback": 0x8a8f96
};

class AppearanceSprite {
  readonly container: Container;
  readonly body: Sprite;
  readonly head: Sprite;
  readonly hair: Sprite;
  readonly leftEye: Sprite;
  readonly rightEye: Sprite;
  readonly marker: Sprite;

  constructor(textures: AppearanceTextures) {
    this.container = new Container();
    this.body = new Sprite(textures.body);
    this.head = new Sprite(textures.head);
    this.hair = new Sprite(textures.hair);
    this.leftEye = new Sprite(textures.eye);
    this.rightEye = new Sprite(textures.eye);
    this.marker = new Sprite(textures.marker);
    for (const child of [this.body, this.head, this.hair, this.leftEye, this.rightEye, this.marker]) {
      child.anchor.set(0.5);
      this.container.addChild(child);
    }
  }

  get visible(): boolean {
    return this.container.visible;
  }

  set visible(value: boolean) {
    this.container.visible = value;
  }

  destroy(): void {
    this.container.destroy({ children: true });
  }
}

/**
 * Pools layered genos containers by descriptor hash. Shared part textures are
 * tinted from palettes, so no per-genos texture is generated.
 */
export class AppearanceSpritePool {
  private readonly pool: DisplayPool<AppearanceSprite>;
  private readonly cache = new Map<string, AppearanceParts>();

  constructor(parent: Container, textures: AppearanceTextures) {
    this.pool = new DisplayPool(() => {
      const sprite = new AppearanceSprite(textures);
      parent.addChild(sprite.container);
      return sprite;
    });
  }

  begin(): void {
    this.pool.begin();
  }

  end(): void {
    this.pool.end();
  }

  acquire(
    appearance: import("../../contracts/hub").AppearanceStatus | null | undefined,
    size: number,
    isPlayer: boolean,
    isLocal: boolean
  ): { container: Container; parts: AppearanceParts } {
    const key = appearanceCacheKey(appearance, isPlayer);
    let parts = this.cache.get(key);
    if (parts === undefined) {
      parts = composeAppearance(appearance, isPlayer);
      if (this.cache.size >= 256) this.cache.delete(this.cache.keys().next().value as string);
      this.cache.set(key, parts);
    }

    const sprite = this.pool.acquire();
    const { container, body, head, hair, leftEye, rightEye, marker } = sprite;
    const scale = parts.heightScale;
    const frame = parts.bodyPart.includes("sturdy") ? 1.08 : parts.bodyPart.includes("slim") ? 0.86 : 1;
    const face = faceScale(parts.facePart);

    body.width = size * 0.78 * frame;
    body.height = size * 1.05 * scale;
    body.y = size * 0.32;
    body.tint = OUTFIT_COLORS[parts.outfitPart] ?? OUTFIT_COLORS["outfit-fallback"]!;

    head.width = size * 0.54 * face.x;
    head.height = size * 0.62 * face.y;
    head.y = -size * 0.18;
    head.tint = SKIN_COLORS[paletteName(parts.skinPalette)] ?? SKIN_COLORS.neutral!;

    hair.visible = parts.hairPart !== "hair-bald";
    hair.width = size * 0.58 * face.x;
    hair.height = size * 0.28;
    hair.y = -size * 0.42;
    hair.tint = HAIR_COLORS[hairColorName(parts.hairPart)] ?? HAIR_COLORS.brown!;

    const eyeTint = EYE_COLORS[parts.eyePart.replace("eyes-", "")] ?? EYE_COLORS.brown!;
    leftEye.tint = eyeTint;
    rightEye.tint = eyeTint;
    leftEye.width = rightEye.width = Math.max(2, size * 0.075);
    leftEye.height = rightEye.height = Math.max(2, size * 0.075);
    leftEye.position.set(-size * 0.12, -size * 0.2);
    rightEye.position.set(size * 0.12, -size * 0.2);

    marker.visible = isPlayer;
    marker.tint = isLocal ? 0x67e8f9 : 0xa78bfa;
    marker.width = marker.height = Math.max(4, size * 0.16);
    marker.position.set(0, -size * 0.7);

    container.position.set(0, 0);
    container.zIndex = isLocal ? 50 : 10;
    for (const part of [parts.bodyPart, parts.skinPalette, parts.facePart, parts.eyePart, parts.hairPart, parts.outfitPart, parts.expressionPart]) {
      if (!isKnownPart(part)) {
        marker.tint = 0xf59e0b;
        marker.visible = true;
        break;
      }
    }

    return { container, parts };
  }

  destroy(): void {
    this.pool.destroy();
    this.cache.clear();
  }
}

function paletteName(palette: string): string {
  return palette.replace("skin-", "");
}

function hairColorName(hair: string): string {
  const index = hair.lastIndexOf("-");
  return index < 0 ? "brown" : hair.slice(index + 1);
}

function faceScale(face: string): { x: number; y: number } {
  switch (face) {
    case "face-round": return { x: 1.1, y: 1.0 };
    case "face-square": return { x: 1.05, y: 0.94 };
    case "face-heart": return { x: 1.08, y: 1.02 };
    case "face-long": return { x: 0.86, y: 1.16 };
    default: return { x: 1, y: 1 };
  }
}

export function createAppearanceTextures(renderer: Renderer): AppearanceTextures {
  return {
    body: generate(renderer, (g) => g.roundRect(-12, -24, 24, 48, 10).fill(0xffffff)),
    head: generate(renderer, (g) => g.ellipse(0, 0, 10, 12).fill(0xffffff)),
    hair: generate(renderer, (g) => g.ellipse(0, 0, 12, 6).fill(0xffffff)),
    eye: generate(renderer, (g) => g.circle(0, 0, 2).fill(0xffffff)),
    marker: generate(renderer, (g) => g.poly([0, -4, 4, 0, 0, 4, -4, 0]).fill(0xffffff))
  };
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
