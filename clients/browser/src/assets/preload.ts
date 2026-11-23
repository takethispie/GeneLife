export interface AssetManifest {
  tileset: string;
  simAtlas: string;
  simPalettes?: string;
  buildingAtlas: string;
  audio: string[];
}

/**
 * The first vertical slice generates its textures procedurally and treats the
 * atlas files as optional art overrides. A missing optional asset produces a
 * visible fallback (procedural colours), never a crash.
 */
export const ASSET_MANIFEST: AssetManifest = {
  tileset: "/atlases/tileset.svg",
  simAtlas: "/atlases/sim.svg",
  simPalettes: "/atlases/sim-palettes.json",
  buildingAtlas: "/atlases/building.svg",
  audio: []
};

export interface AssetLoadResult {
  loaded: string[];
  missing: string[];
}

export type FetchLike = (input: string) => Promise<{ ok: boolean }>;

/**
 * Verifies declared assets before the render ticker starts. Failures are
 * reported to the caller so the bootstrap can log/announce them.
 */
export async function preloadAssets(
  manifest: AssetManifest,
  fetchImpl: FetchLike = fetch
): Promise<AssetLoadResult> {
  const urls = [
    manifest.tileset,
    manifest.simAtlas,
    ...(manifest.simPalettes === undefined ? [] : [manifest.simPalettes]),
    manifest.buildingAtlas,
    ...manifest.audio
  ];
  const loaded: string[] = [];
  const missing: string[] = [];

  await Promise.all(
    urls.map(async (url) => {
      try {
        const response = await fetchImpl(url);
        if (response.ok) loaded.push(url);
        else missing.push(url);
      } catch {
        missing.push(url);
      }
    })
  );

  return { loaded, missing };
}
