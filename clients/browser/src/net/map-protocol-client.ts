import type { MapChunkBatch, MapManifest, MapOverview } from "../contracts/hub";
import { MAP_TILE_ENCODING_VERSION } from "../contracts/hub";
import type { ChunkRect, TileRect } from "../simulation/map/chunk-protocol";
import { chunksForTileRect } from "../simulation/map/chunk-protocol";

/** Fetch signature, injected so the client is testable without a live server. */
export type MapProtocolFetch = (input: string, init?: RequestInit) => Promise<Response>;

export interface MapProtocolClientOptions {
  baseUrl: string;
  fetch?: MapProtocolFetch;
  /** Optional bearer token supplier (the map endpoints require the Player policy). */
  token?: () => string | null;
}

/** A defined server error (E-10): a status plus the machine-readable `code` when present. */
export class MapProtocolError extends Error {
  readonly status: number;
  readonly code: string | null;
  /** Present on `spec-mismatch`: the current spec hash the client should re-fetch. */
  readonly specHash: string | null;

  constructor(status: number, code: string | null, message: string, specHash: string | null = null) {
    super(message);
    this.name = "MapProtocolError";
    this.status = status;
    this.code = code;
    this.specHash = specHash;
  }

  /** True when the world was regenerated and the client must re-fetch the manifest. */
  get isSpecMismatch(): boolean {
    return this.code === "spec-mismatch";
  }
}

/**
 * The browser's viewport-request path for the Phase 3 chunk protocol.
 *
 * Transport is plain HTTP GET — the payloads are cache-friendly, idempotent and
 * byte-stable per `(specHash, cx, cy)`. Batching is one request per viewport:
 * {@link loadViewport} derives the whole chunk set from the camera rect (no round
 * trip) and fetches it in ONE call, rather than one request per chunk.
 *
 * The manifest is fetched once and cached; a `spec-mismatch` error tells the
 * caller to re-fetch it. The overview rides its own endpoint, fetched once when
 * the map opens.
 */
export class MapProtocolClient {
  private readonly baseUrl: string;
  private readonly fetchImpl: MapProtocolFetch;
  private readonly token: () => string | null;

  constructor(options: MapProtocolClientOptions) {
    this.baseUrl = options.baseUrl.replace(/\/+$/, "");
    this.fetchImpl = options.fetch ?? defaultFetch();
    this.token = options.token ?? (() => null);
  }

  async fetchManifest(): Promise<MapManifest> {
    return this.getJson<MapManifest>("/api/map/manifest");
  }

  async fetchOverview(specHash?: string): Promise<MapOverview> {
    const query = specHash === undefined ? "" : `?specHash=${encodeURIComponent(specHash)}`;
    return this.getJson<MapOverview>(`/api/map/overview${query}`);
  }

  /** One batched request for a chunk rectangle. */
  async fetchChunks(rect: ChunkRect, specHash?: string): Promise<MapChunkBatch> {
    const params = new URLSearchParams({
      cxMin: String(rect.cxMin),
      cyMin: String(rect.cyMin),
      cxMax: String(rect.cxMax),
      cyMax: String(rect.cyMax)
    });
    if (specHash !== undefined) params.set("specHash", specHash);
    return this.getJson<MapChunkBatch>(`/api/map/chunks?${params.toString()}`);
  }

  /**
   * The browser viewport request: derive the chunk set from a camera tile rect
   * and fetch it in one batch, carrying the manifest's spec hash so a stale
   * request is refused rather than mis-merged.
   */
  async loadViewport(tileRect: TileRect, manifest: MapManifest): Promise<MapChunkBatch> {
    const rect = chunksForTileRect(tileRect, manifest);
    return this.fetchChunks(rect, manifest.specHash);
  }

  private async getJson<T>(path: string): Promise<T> {
    const headers: Record<string, string> = { Accept: "application/json" };
    const token = this.token();
    if (token !== null && token !== "") headers.Authorization = `Bearer ${token}`;

    const response = await this.fetchImpl(`${this.baseUrl}${path}`, { headers });
    if (!response.ok) throw await toError(response);
    return (await response.json()) as T;
  }
}

async function toError(response: Response): Promise<MapProtocolError> {
  let code: string | null = null;
  let message = `Map request failed (${response.status})`;
  let specHash: string | null = null;
  try {
    const body = (await response.json()) as { code?: string; message?: string; specHash?: string };
    code = body.code ?? null;
    message = body.message ?? message;
    specHash = body.specHash ?? null;
  } catch {
    // Body was not JSON; keep the status-derived message.
  }
  return new MapProtocolError(response.status, code, message, specHash);
}

function defaultFetch(): MapProtocolFetch {
  const impl = globalThis.fetch;
  if (impl === undefined) {
    throw new Error("No fetch implementation available; pass options.fetch.");
  }
  return (input, init) => impl(input, init);
}

/** The encoding version the client accepts; a different value means refuse cleanly. */
export function acceptsTileEncoding(manifest: MapManifest): boolean {
  return manifest.tileEncodingVersion === MAP_TILE_ENCODING_VERSION;
}
