import { describe, expect, it, vi } from "vitest";
import { ASSET_MANIFEST, preloadAssets } from "./preload";

describe("preloadAssets", () => {
  it("reports loaded and missing assets", async () => {
    const fetchImpl = vi.fn(async (url: string) => ({ ok: !url.includes("sim") }));
    const result = await preloadAssets(ASSET_MANIFEST, fetchImpl);
    expect(result.loaded).toContain("/atlases/tileset.svg");
    expect(result.missing).toContain("/atlases/sim.svg");
  });

  it("treats network errors as missing", async () => {
    const fetchImpl = vi.fn(async () => {
      throw new Error("offline");
    });
    const result = await preloadAssets(ASSET_MANIFEST, fetchImpl);
    expect(result.missing).toHaveLength(3);
    expect(result.loaded).toHaveLength(0);
  });
});
