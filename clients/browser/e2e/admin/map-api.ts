import type { Page, Request, Route } from "@playwright/test";

export interface MapApiCall {
  method: string;
  pathname: string;
  body: unknown;
}

export interface MapApiHandle {
  calls: MapApiCall[];
  created: boolean;
}

type FulfillOptions = Parameters<Route["fulfill"]>[0];

const initialMap = {
  hasMap: true,
  mapId: "world",
  version: 4,
  layoutVersion: 2,
  seed: 11,
  width: 256,
  height: 256,
  tileSize: 1,
  minX: -128,
  minY: -128,
  maxX: 127,
  maxY: 127,
  buildings: 42,
  occupiedTiles: 168,
  reservedTiles: 4,
  unplacedBuildings: 0,
  healthIssues: 0,
  buildingList: [],
  unplaced: []
};

const createdMap = {
  ...initialMap,
  version: 5,
  layoutVersion: 3,
  seed: 77,
  width: 192,
  height: 192,
  minX: -96,
  minY: -96,
  maxX: 95,
  maxY: 95,
  buildings: 30,
  occupiedTiles: 120
};

const world = {
  tick: 120,
  year: 1,
  month: 1,
  isRunning: true,
  capturedAt: new Date().toISOString(),
  sims: [
    {
      networkId: 1,
      name: "Alice",
      age: 30,
      isPlayerControlled: true,
      isDead: false,
      tileX: 0,
      tileY: 0,
      x: 0,
      y: 0
    }
  ],
  companies: [],
  offices: [],
  employees: [],
  jobPostings: [],
  jobApplications: [],
  openPositions: [],
  economy: {},
  housing: {},
  lifecycle: {},
  recentLedger: []
};

/**
 * Mocks the admin map endpoints. `hasMap` controls whether the first load
 * reports an existing world, and a successful create flips subsequent loads to
 * the regenerated map.
 */
export async function mockMapApi(
  page: Page,
  options: { hasMap?: boolean } = {}
): Promise<MapApiHandle> {
  const calls: MapApiCall[] = [];
  const handle: MapApiHandle = { calls, created: false };
  let hasMap = options.hasMap ?? true;

  await page.route("**/appsettings*.json", async (route) => {
    const origin = new URL(route.request().url()).origin;
    await route.fulfill({
      json: { AdminPlayerId: "admin-ui", ApiBaseUrl: origin }
    });
  });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const pathname = url.pathname;
    const call: MapApiCall = {
      method: request.method(),
      pathname,
      body: readBody(request)
    };
    calls.push(call);

    const response = respond(call, { hasMap, created: handle.created });
    if (response === null) {
      await route.fulfill({
        status: 404,
        contentType: "application/json",
        body: JSON.stringify({ error: "unmocked", pathname })
      });
      return;
    }

    if (call.method === "POST" && pathname === "/api/map/create") {
      handle.created = true;
      hasMap = true;
    }

    await route.fulfill(response);
  });

  return handle;
}

function readBody(request: Request): unknown {
  try {
    return request.postDataJSON();
  } catch {
    return request.postData();
  }
}

function respond(
  call: MapApiCall,
  state: { hasMap: boolean; created: boolean }
): FulfillOptions | null {
  const { method, pathname } = call;

  if (method === "OPTIONS") return { status: 204 };

  if (method === "POST" && pathname === "/api/auth/local-admin") {
    return json({ accessToken: "e2e-admin-token", expiresAt: "2099-01-01T00:00:00+00:00" });
  }

  if (method === "GET" && pathname === "/api/map") {
    if (!state.hasMap)
      return json({ ...initialMap, hasMap: false, mapId: "", version: 0 });

    return json(state.created ? createdMap : initialMap);
  }

  if (method === "GET" && pathname === "/api/simulation/status") {
    return json({ isRunning: state.hasMap });
  }

  if (method === "GET" && pathname === "/api/data") {
    return json({ ...world, isRunning: state.hasMap });
  }

  if (method === "POST" && pathname === "/api/map/create") {
    return json({
      mapId: "world",
      version: createdMap.version,
      layoutVersion: createdMap.layoutVersion,
      seed: createdMap.seed,
      width: createdMap.width,
      height: createdMap.height,
      buildings: createdMap.buildings,
      population: 24,
      players: 1,
      elapsedMs: 321
    });
  }

  return null;
}

function json(body: unknown, status = 200): FulfillOptions {
  return {
    status,
    contentType: "application/json",
    body: JSON.stringify(body)
  };
}
