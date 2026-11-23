import type { AppearanceStatus } from "../../contracts/hub";

export interface AppearanceParts {
  bodyPart: string;
  skinPalette: string;
  facePart: string;
  eyePart: string;
  hairPart: string;
  outfitPart: string;
  expressionPart: string;
  heightScale: number;
  layerOrderVersion: number;
  hash: number;
}

const SKIN_TONE = ["porcelain", "fair", "olive", "tan", "brown", "deep"] as const;
const FACE_SHAPE = ["oval", "round", "square", "heart", "long"] as const;
const HAIR_COLOR = ["black", "brown", "blonde", "ginger", "grey"] as const;
const HAIR_STYLE = ["short", "cropped", "wavy", "straight", "curly", "braided", "bald"] as const;
const EYE_COLOR = ["brown", "blue", "green", "grey", "hazel"] as const;
const BODY_FRAME = ["slim", "average", "sturdy"] as const;
const AGE_BAND = ["child", "teen", "adult", "middle", "senior"] as const;
const EXPRESSION = ["calm", "warm", "serious", "alert"] as const;

export const LAYER_ORDER_VERSION = 1;

/** Pure mirror of the server's AppearanceComposer part/palette contract. */
export function composeAppearance(
  appearance: AppearanceStatus | null | undefined,
  isPlayer = false
): AppearanceParts {
  const safe = sanitize(appearance);
  const band = AGE_BAND[safe.ageBand] ?? "adult";
  const style = HAIR_STYLE[safe.hairStyle] ?? "short";
  const parts: AppearanceParts = {
    bodyPart: `body-${band}-${BODY_FRAME[safe.bodyFrame] ?? "average"}`,
    skinPalette: `skin-${SKIN_TONE[safe.skinTone] ?? "olive"}`,
    facePart: `face-${FACE_SHAPE[safe.faceShape] ?? "oval"}`,
    eyePart: `eyes-${EYE_COLOR[safe.eyeColor] ?? "brown"}`,
    hairPart: style === "bald" ? "hair-bald" : `hair-${style}-${HAIR_COLOR[safe.hairColor] ?? "brown"}`,
    outfitPart: outfitPart(band, isPlayer),
    expressionPart: `expression-${EXPRESSION[safe.expression] ?? "calm"}`,
    heightScale: Math.max(0.5, Math.min(1.1, (safe.heightScale || 80) / 100)),
    layerOrderVersion: LAYER_ORDER_VERSION,
    hash: 0
  };
  parts.hash = stableHash(JSON.stringify([
    parts.bodyPart, parts.skinPalette, parts.facePart, parts.eyePart,
    parts.hairPart, parts.outfitPart, parts.expressionPart,
    Math.round(parts.heightScale * 100), parts.layerOrderVersion
  ]));
  return parts;
}

export function isKnownPart(part: string | null | undefined): boolean {
  return typeof part === "string" && /^[a-z0-9]+(-[a-z0-9]+)+$/.test(part);
}

export function appearanceCacheKey(appearance: AppearanceStatus | null | undefined, isPlayer: boolean): string {
  if (appearance === null || appearance === undefined) return isPlayer ? "fallback:player" : "fallback";
  return [
    appearance.skinTone, appearance.faceShape, appearance.hairColor, appearance.hairStyle,
    appearance.eyeColor, appearance.bodyFrame, appearance.heightScale, appearance.ageBand,
    appearance.expression, appearance.appearanceVersion, isPlayer ? 1 : 0
  ].join(":");
}

function sanitize(appearance: AppearanceStatus | null | undefined): AppearanceStatus {
  const fallback: AppearanceStatus = {
    skinTone: 2, faceShape: 0, hairColor: 1, hairStyle: 0, eyeColor: 0,
    bodyFrame: 1, heightScale: 80, ageBand: 2, expression: 0, appearanceVersion: 1
  };
  if (appearance === null || appearance === undefined) return fallback;
  return {
    skinTone: clampInt(appearance.skinTone, SKIN_TONE.length),
    faceShape: clampInt(appearance.faceShape, FACE_SHAPE.length),
    hairColor: clampInt(appearance.hairColor, HAIR_COLOR.length),
    hairStyle: clampInt(appearance.hairStyle, HAIR_STYLE.length),
    eyeColor: clampInt(appearance.eyeColor, EYE_COLOR.length),
    bodyFrame: clampInt(appearance.bodyFrame, BODY_FRAME.length),
    heightScale: clampInt(appearance.heightScale, 101),
    ageBand: clampInt(appearance.ageBand, AGE_BAND.length),
    expression: clampInt(appearance.expression, EXPRESSION.length),
    appearanceVersion: Math.max(1, Math.trunc(appearance.appearanceVersion))
  };
}

function clampInt(value: number, length: number): number {
  if (!Number.isFinite(value)) return 0;
  const integer = Math.trunc(value);
  return Math.max(0, Math.min(length - 1, integer));
}

function outfitPart(band: string, isPlayer: boolean): string {
  if (isPlayer) return "outfit-player";
  switch (band) {
    case "child": return "outfit-child";
    case "teen": return "outfit-student";
    case "senior": return "outfit-senior";
    default: return "outfit-worker";
  }
}

function stableHash(value: string): number {
  let hash = 2166136261;
  for (let i = 0; i < value.length; i++) {
    hash ^= value.charCodeAt(i);
    hash = Math.imul(hash, 16777619);
  }
  return hash >>> 0;
}
