import type { Page, Request, Route } from "@playwright/test";

export interface ApiCall {
  method: string;
  pathname: string;
  search: string;
  body: unknown;
}

export interface MockFailure {
  method: string;
  pathname: string | RegExp;
  status?: number;
  body?: string;
}

export interface PagesApiOptions {
  /** Exact-pathname response overrides, merged over the built-in defaults. */
  responses?: Record<string, unknown>;
  /** Injected failures, matched before the response table. */
  failures?: MockFailure[];
  /** Override the simulation status payload. */
  simulation?: unknown;
}

export interface PagesApiHandle {
  calls: ApiCall[];
  /** Requests that matched no route — tests assert this stays empty. */
  unhandled: ApiCall[];
}

type FulfillOptions = Parameters<Route["fulfill"]>[0];

const tokenResponse = {
  accessToken: "e2e-admin-token",
  expiresAt: "2099-01-01T00:00:00+00:00"
};

/**
 * The default `/api/data` payload. It carries one NPC and one player-controlled
 * genos plus a single company/office/employee so the Data and Economy pages have
 * something concrete to render.
 */
const worldData = {
  tick: 1234,
  year: 5,
  month: 3,
  isRunning: true,
  capturedAt: "2026-01-01T00:00:00+00:00",
  genoses: [
    {
      networkId: 1,
      name: "Ada Lovelace",
      age: 30,
      isPlayerControlled: false,
      currentAction: "Working",
      stage: "Work",
      tenure: "Owner",
      housingTier: "Comfortable"
    },
    {
      networkId: 2,
      name: "Bex Turner",
      age: 22,
      isPlayerControlled: true,
      currentAction: "Idle",
      stage: "Work",
      tenure: "Tenant",
      housingTier: "Basic"
    }
  ],
  companies: [
    {
      networkId: 10,
      name: "Acme Manufacturing",
      employeeCount: 4,
      officeCount: 1,
      balance: 1000,
      solvency: "Healthy"
    }
  ],
  offices: [
    {
      networkId: 11,
      name: "Acme HQ",
      companyNetworkId: 10,
      company: "Acme Manufacturing",
      capacity: 10,
      employeeCount: 4
    }
  ],
  employees: [
    {
      genosNetworkId: 1,
      genos: "Ada Lovelace",
      companyNetworkId: 10,
      company: "Acme Manufacturing",
      position: "Engineer",
      salary: 300
    }
  ],
  jobPostings: [],
  jobApplications: [],
  openPositions: [],
  economy: {
    year: 5,
    month: 3,
    householdCash: 100,
    householdSavings: 50,
    companyCash: 1000,
    companyDebt: 0,
    pensionFund: 10,
    governmentFund: 20,
    bankFunds: 5,
    totalMoneySupply: 1185,
    arrears: 0,
    wagesPaidThisYear: 600,
    revenueThisYear: 900,
    taxThisYear: 90,
    basicIncomeThisYear: 12,
    unemploymentRate: 0.05,
    vacancyRate: 0.1,
    wageShare: 0.55,
    health: [{ name: "Money conservation", level: "Pass", detail: "Money supply is conserved." }]
  },
  housing: {
    properties: 1,
    units: 2,
    occupiedUnits: 1,
    vacantUnits: 1,
    vacancyRate: 0.5,
    rentIndex: 1.2,
    health: []
  },
  lifecycle: {
    totalGenoses: 2,
    education: 0,
    work: 1,
    retirement: 0,
    elderly: 0,
    averageAge: 26,
    health: []
  },
  map: null,
  recentLedger: []
};

const housingOverview = {
  properties: 3,
  units: 12,
  occupiedUnits: 9,
  vacantUnits: 3,
  shelterBeds: 4,
  shelteredHouseholds: 1,
  homelessHouseholds: 0,
  overcrowdedUnits: 1,
  unitsBelowCondition: 2,
  activeProjects: 1,
  constructionUnits: 4,
  vacancyRate: 0.25,
  rentIndex: 1.2,
  rentRoll: 5400,
  rentCollectedThisMonth: 5400,
  utilityCollectedThisMonth: 900,
  upkeepPaidThisMonth: 300,
  rentArrears: 120,
  utilityArrears: 30,
  medianRentBurden: 0.31,
  shelterOccupancy: 0.25,
  evictionsThisYear: 1,
  movesThisYear: 6,
  year: 5,
  month: 3,
  health: [{ name: "Vacancy rate", level: "Pass", detail: "Vacancy is inside the band." }]
};

const lifecycleOverview = {
  status: {
    totalGenoses: 120,
    education: 18,
    work: 70,
    retirement: 20,
    elderly: 12,
    averageAge: 34.5,
    medianAge: 33,
    averageHealth: 88.2,
    workingAgeShare: 0.58,
    oldAgeDependencyRatio: 0.27,
    studentsEnrolled: 16,
    unenrolledStudents: 2,
    enrollmentRate: 0.88,
    graduatesThisYear: 3,
    employed: 66,
    unemployed: 4,
    unemploymentRate: 0.057,
    retiredThisYear: 2,
    deathsThisYear: 1,
    arrivalsThisYear: 5,
    lifeExpectancy: 78.4,
    mortalityRecords: 1,
    pensionFund: 4000,
    pensionLiabilities: 5000,
    pensionFundingRatio: 0.8,
    averagePension: 120,
    pensionReplacementRate: 0.45,
    schools: 2,
    policy: { schoolStartAge: 6, workStartAge: 18, retirementAge: 65, elderlyAge: 75, maxAge: 100 },
    immigration: {
      targetPopulation: 150,
      enabled: true,
      arrivalsThisYear: 5,
      arrivalBudget: 10,
      pending: 0,
      cancelledThisYear: 0,
      paused: false
    },
    latestSnapshot: null,
    health: [{ name: "Pension funding", level: "Warn", detail: "Funding ratio below target." }]
  },
  schools: [],
  pensions: {},
  mortality: [],
  populationHistory: []
};

const playerOverview = {
  players: 3,
  active: 2,
  awaitingContinuation: 1,
  employed: 2,
  housed: 2,
  pendingInvitations: 0,
  deathsThisYear: 0,
  continuationsThisYear: 1,
  commandsAppliedThisYear: 4
};

const consumptionOverview = {
  networks: [
    {
      networkId: 1,
      service: "Water",
      production: 100,
      capacity: 120,
      load: 80,
      reserve: 20,
      state: "Stable"
    }
  ],
  households: 5,
  cutOffHouseholds: 1,
  reducedHouseholds: 1,
  stores: 1,
  stockUnits: 40,
  spoiledThisYear: 2,
  nutrition: [{ band: "Good", count: 4 }],
  malnourishedHouseholds: 1,
  groceryCatalogVersion: 3
};

/**
 * Default response table. `path` is an exact pathname or a RegExp; `body` is the
 * JSON payload (arrays for list endpoints). Order matters: first match wins.
 */
const table: { path: string | RegExp; body: unknown }[] = [
  { path: "/api/simulation/status", body: { isRunning: true } },
  { path: "/api/data", body: worldData },
  { path: "/api/economy/overview", body: worldData.economy },
  { path: "/api/economy/fiscal", body: { governmentBalance: 200, pensionBalance: 80 } },
  { path: "/api/economy/metrics/history", body: [] },
  { path: "/api/economy/ledger", body: [] },
  { path: "/api/housing/overview", body: housingOverview },
  { path: "/api/housing/properties", body: [] },
  { path: "/api/housing/households", body: [] },
  { path: "/api/housing/rent-roll", body: [] },
  { path: "/api/housing/construction", body: [] },
  { path: "/api/housing/metrics/history", body: [] },
  { path: "/api/lifecycle/overview", body: lifecycleOverview },
  { path: "/api/lifecycle/genoses", body: [] },
  { path: "/api/player", body: playerOverview },
  { path: "/api/player/lives", body: { items: [], total: 0 } },
  { path: /^\/api\/player\/lives\/[^/]+$/, body: null },
  { path: "/api/appearance", body: { items: [], total: 0 } },
  { path: "/api/consumption", body: consumptionOverview },
  { path: "/api/consumption/households", body: { items: [], total: 0 } },
  { path: "/api/consumption/stores", body: { items: [], total: 0 } },
  { path: "/api/map", body: { hasMap: false, width: 0, height: 0 } }
];

export async function mockAdminPagesApi(
  page: Page,
  options: PagesApiOptions = {}
): Promise<PagesApiHandle> {
  const calls: ApiCall[] = [];
  const unhandled: ApiCall[] = [];

  await page.route("**/appsettings*.json", async (route) => {
    const origin = new URL(route.request().url()).origin;
    await route.fulfill({ json: { AdminPlayerId: "admin-ui", ApiBaseUrl: origin } });
  });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    const url = new URL(request.url());
    const call: ApiCall = {
      method: request.method(),
      pathname: url.pathname,
      search: url.search,
      body: readBody(request)
    };
    calls.push(call);

    const failure = options.failures?.find((candidate) => matchesFailure(candidate, call));
    if (failure) {
      await route.fulfill({
        status: failure.status ?? 500,
        contentType: "text/plain",
        body: failure.body ?? "injected failure"
      });
      return;
    }

    const response = respond(call, options);
    if (response === null) {
      unhandled.push(call);
      await route.fulfill({
        status: 404,
        contentType: "application/json",
        body: JSON.stringify({ error: "unmocked", pathname: call.pathname })
      });
      return;
    }

    await route.fulfill(response);
  });

  return { calls, unhandled };
}

function matchesFailure(failure: MockFailure, call: ApiCall): boolean {
  if (failure.method !== call.method) return false;
  return typeof failure.pathname === "string"
    ? failure.pathname === call.pathname
    : failure.pathname.test(call.pathname);
}

function readBody(request: Request): unknown {
  try {
    return request.postDataJSON();
  } catch {
    return request.postData();
  }
}

function respond(call: ApiCall, options: PagesApiOptions): FulfillOptions | null {
  const { method, pathname } = call;

  if (method === "OPTIONS") return { status: 204 };

  if (method === "POST" && pathname === "/api/auth/local-admin") {
    return jsonResponse(tokenResponse);
  }

  if (method === "POST" && pathname === "/api/auth/token") {
    return jsonResponse(tokenResponse);
  }

  if (method === "GET") {
    if (pathname === "/api/simulation/status" && options.simulation !== undefined) {
      return jsonResponse(options.simulation);
    }
    if (pathname in (options.responses ?? {})) {
      return jsonResponse((options.responses as Record<string, unknown>)[pathname]);
    }
    const body = lookupDefault(pathname);
    if (body !== undefined) return jsonResponse(body);
  }

  // Admin commands (POST/PATCH/DELETE under /api) succeed with 202 Accepted,
  // matching the API's command pattern. The response body is ignored by the UI.
  if (method !== "GET" && method !== "OPTIONS" && pathname.startsWith("/api/")) {
    return { status: 202, body: "" };
  }

  return null;
}

function lookupDefault(pathname: string): unknown | undefined {
  for (const entry of table) {
    const matched =
      typeof entry.path === "string" ? entry.path === pathname : entry.path.test(pathname);
    if (matched) return entry.body;
  }
  return undefined;
}

function jsonResponse(body: unknown, status = 200): FulfillOptions {
  return {
    status,
    contentType: "application/json",
    body: JSON.stringify(body)
  };
}
