import { describe, expect, it, vi } from "vitest";
import type { MapManifest } from "../contracts/hub";
import { acceptsTileEncoding, MapProtocolClient, MapProtocolError } from "./map-protocol-client";

const manifest: MapManifest = {
  hasMap: true,
  specHash: "spec-1",
  width: 32000,
  height: 32000,
  chunkSize: 64,
  tileEncodingVersion: 1,
  minX: -16000,
  minY: -16000,
  maxX: 15999,
  maxY: 15999,
  chunksPerSideX: 500,
  chunksPerSideY: 500
};

function stubResponse(body: unknown, status = 200): Response {
  return {
    ok: status >= 200 && status < 300,
    status,
    json: async () => body
  } as unknown as Response;
}

function client(fetchImpl: (input: string, init?: RequestInit) => Promise<Response>, token?: () => string | null) {
  return new MapProtocolClient({ baseUrl: "http://localhost:5288/", fetch: fetchImpl, token });
}

describe("MapProtocolClient", () => {
  it("fetches the manifest from the versioned endpoint", async () => {
    const fetchImpl = vi.fn(async (_input: string, _init?: RequestInit) => stubResponse(manifest));
    const result = await client(fetchImpl).fetchManifest();

    expect(result).toEqual(manifest);
    expect(fetchImpl).toHaveBeenCalledTimes(1);
    expect(fetchImpl.mock.calls[0]![0]).toBe("http://localhost:5288/api/map/manifest");
  });

  it("sends a bearer token when one is supplied", async () => {
    const fetchImpl = vi.fn(async (_input: string, _init?: RequestInit) => stubResponse(manifest));
    await client(fetchImpl, () => "secret").fetchManifest();

    const init = fetchImpl.mock.calls[0]![1] as RequestInit;
    expect((init.headers as Record<string, string>).Authorization).toBe("Bearer secret");
  });

  it("issues ONE batched request per viewport", async () => {
    const batch = {
      specHash: "spec-1",
      tileEncodingVersion: 1,
      chunkSize: 64,
      minX: -16000,
      minY: -16000,
      maxX: -16000 + 130,
      maxY: -16000 + 130,
      chunks: [],
      buildings: []
    };
    const fetchImpl = vi.fn(async (_input: string, _init?: RequestInit) => stubResponse(batch));

    const result = await client(fetchImpl).loadViewport(
      { minX: -16000, minY: -16000, maxX: -16000 + 130, maxY: -16000 + 130 },
      manifest
    );

    expect(result).toEqual(batch);
    expect(fetchImpl).toHaveBeenCalledTimes(1);
    const url = fetchImpl.mock.calls[0]![0] as string;
    expect(url).toContain("/api/map/chunks?");
    expect(url).toContain("cxMin=0");
    expect(url).toContain("cyMin=0");
    expect(url).toContain("cxMax=2");
    expect(url).toContain("cyMax=2");
    expect(url).toContain("specHash=spec-1");
  });

  it("fetches the overview on its own endpoint", async () => {
    const overview = {
      specHash: "spec-1",
      chunkSize: 64,
      width: 32000,
      height: 32000,
      columns: 500,
      rows: 500,
      dominant: [{ row: 0, start: 0, length: 500, terrain: "Land" }]
    };
    const fetchImpl = vi.fn(async (_input: string, _init?: RequestInit) => stubResponse(overview));

    const result = await client(fetchImpl).fetchOverview("spec-1");

    expect(result).toEqual(overview);
    expect(fetchImpl.mock.calls[0]![0]).toBe("http://localhost:5288/api/map/overview?specHash=spec-1");
  });

  it("surfaces a spec mismatch as a defined error", async () => {
    const fetchImpl = vi.fn(async () =>
      stubResponse({ code: "spec-mismatch", message: "regenerated", specHash: "spec-2" }, 409)
    );

    await expect(client(fetchImpl).fetchChunks({ cxMin: 0, cyMin: 0, cxMax: 0, cyMax: 0 }, "spec-1")).rejects.toMatchObject({
      name: "MapProtocolError",
      status: 409,
      code: "spec-mismatch",
      specHash: "spec-2"
    });
  });

  it("keeps a status-derived message when the error body is not JSON", async () => {
    const fetchImpl = vi.fn(async () => ({
      ok: false,
      status: 400,
      json: async () => {
        throw new Error("not json");
      }
    } as unknown as Response));

    const error = await client(fetchImpl)
      .fetchChunks({ cxMin: 0, cyMin: 0, cxMax: 0, cyMax: 0 })
      .catch((err: unknown) => err);

    expect(error).toBeInstanceOf(MapProtocolError);
    expect((error as MapProtocolError).message).toContain("400");
    expect((error as MapProtocolError).code).toBeNull();
  });

  it("accepts only the known tile encoding version", () => {
    expect(acceptsTileEncoding(manifest)).toBe(true);
    expect(acceptsTileEncoding({ ...manifest, tileEncodingVersion: 2 })).toBe(false);
  });
});
