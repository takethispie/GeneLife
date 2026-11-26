import { describe, expect, it } from "vitest";
import { composeAppearance, isKnownPart } from "./parts";
import type { AppearanceStatus } from "../../contracts/hub";
import appearanceFixture from "../../test/fixtures/world-snapshot-appearance.json";

/**
 * Guards the appearance fixture set itself: every appearance the fixture can
 * produce must compose into parts the renderer recognises. `AppearanceSpritePool`
 * paints an amber marker over any genos whose parts fail `isKnownPart`, so an
 * unknown part is a silent visual defect rather than a crash.
 */
describe("appearance fixture set", () => {
  const genoses = appearanceFixture.genoses as Array<{
    networkId: number;
    name: string;
    isPlayerControlled: boolean;
    appearance: AppearanceStatus | null;
  }>;

  it("covers every value of every appearance dimension", () => {
    const dimensions = [
      "skinTone",
      "faceShape",
      "hairColor",
      "hairStyle",
      "eyeColor",
      "bodyFrame",
      "ageBand",
      "expression"
    ] as const;
    const expected = {
      skinTone: 6,
      faceShape: 5,
      hairColor: 5,
      hairStyle: 7,
      eyeColor: 5,
      bodyFrame: 3,
      ageBand: 5,
      expression: 4
    };

    for (const dimension of dimensions) {
      const seen = new Set(genoses.map((genos) => genos.appearance?.[dimension]));
      expect(seen.size, `${dimension} coverage`).toBe(expected[dimension]);
    }
  });

  it("composes only known parts for every genos", () => {
    const unknown: string[] = [];

    for (const genos of genoses) {
      const parts = composeAppearance(genos.appearance, genos.isPlayerControlled);
      for (const part of [
        parts.bodyPart,
        parts.skinPalette,
        parts.facePart,
        parts.eyePart,
        parts.hairPart,
        parts.outfitPart,
        parts.expressionPart
      ]) {
        if (!isKnownPart(part)) unknown.push(`${genos.name}: ${part}`);
      }
    }

    expect(unknown).toEqual([]);
  });
});
