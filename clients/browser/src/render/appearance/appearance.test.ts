import { describe, expect, it } from "vitest";
import type { AppearanceStatus } from "../../contracts/hub";
import {
  appearanceCacheKey,
  composeAppearance,
  isKnownPart,
  LAYER_ORDER_VERSION
} from "./parts";

const base: AppearanceStatus = {
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

describe("appearance composition", () => {
  it("composes every age band and body frame with known part ids", () => {
    for (let ageBand = 0; ageBand < 5; ageBand++) {
      for (let bodyFrame = 0; bodyFrame < 3; bodyFrame++) {
        const parts = composeAppearance({ ...base, ageBand, bodyFrame });
        for (const part of [
          parts.bodyPart, parts.skinPalette, parts.facePart, parts.eyePart,
          parts.hairPart, parts.outfitPart, parts.expressionPart
        ]) {
          expect(isKnownPart(part), part).toBe(true);
        }
        expect(parts.layerOrderVersion).toBe(LAYER_ORDER_VERSION);
        expect(parts.heightScale).toBeCloseTo(0.88);
      }
    }
  });

  it("is deterministic and changes the hash when an inherited field changes", () => {
    expect(composeAppearance(base)).toEqual(composeAppearance(base));
    expect(composeAppearance({ ...base, skinTone: 5 }).hash)
      .not.toBe(composeAppearance(base).hash);
    expect(composeAppearance({ ...base, hairStyle: 6 }).hairPart).toBe("hair-bald");
  });

  it("falls back to a neutral descriptor for missing or malformed values", () => {
    const missing = composeAppearance(null);
    expect(missing.bodyPart).toBe("body-adult-average");
    expect(missing.skinPalette).toBe("skin-olive");
    expect(missing.outfitPart).toBe("outfit-worker");

    const malformed = composeAppearance({ ...base, skinTone: 99, ageBand: 99 });
    expect(malformed.skinPalette).toBe("skin-deep");
    expect(malformed.bodyPart).toBe("body-senior-average");
    expect(isKnownPart(malformed.bodyPart)).toBe(true);
  });

  it("marks local players with a non-appearance outfit and cache key", () => {
    const local = composeAppearance(base, true);
    expect(local.outfitPart).toBe("outfit-player");
    expect(appearanceCacheKey(base, true)).not.toBe(appearanceCacheKey(base, false));
    expect(appearanceCacheKey(null, false)).toBe("fallback");
  });
});
