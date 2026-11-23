import type { Page, Request, Route } from "@playwright/test";
import {
  familyAttraction,
  familyChildrenPage,
  familyGuardianships,
  familyOverview,
  familyPregnanciesPage,
  familyRelationshipsPage,
  familySimDetail,
  familySimsPage
} from "./family-fixtures";

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

export interface FamilyApiOptions {
  family?: unknown;
  sims?: unknown;
  simDetail?: unknown;
  relationships?: unknown;
  attraction?: unknown;
  pregnancies?: unknown;
  children?: unknown;
  guardianships?: unknown;
  simulation?: unknown;
  failures?: MockFailure[];
}

export interface FamilyApiHandle {
  calls: ApiCall[];
  unhandled: ApiCall[];
}

type FulfillOptions = Parameters<Route["fulfill"]>[0];

export async function mockFamilyApi(
  page: Page,
  options: FamilyApiOptions = {}
): Promise<FamilyApiHandle> {
  const calls: ApiCall[] = [];
  const unhandled: ApiCall[] = [];

  await page.route("**/appsettings*.json", async (route) => {
    const origin = new URL(route.request().url()).origin;
    await route.fulfill({
      json: { AdminPlayerId: "admin-ui", ApiBaseUrl: origin }
    });
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

function respond(call: ApiCall, options: FamilyApiOptions): FulfillOptions | null {
  const { method, pathname } = call;

  if (method === "OPTIONS") return { status: 204 };

  if (method === "POST" && pathname === "/api/auth/local-admin") {
    return jsonResponse({
      accessToken: "e2e-admin-token",
      expiresAt: "2099-01-01T00:00:00+00:00"
    });
  }

  if (method === "GET" && pathname === "/api/simulation/status") {
    return jsonResponse(options.simulation ?? { isRunning: true });
  }

  if (method === "GET" && pathname === "/api/family") {
    return jsonResponse(options.family ?? familyOverview);
  }

  if (method === "GET" && pathname === "/api/family/sims") {
    return jsonResponse(options.sims ?? familySimsPage);
  }

  if (method === "GET" && /^\/api\/family\/sims\/\d+$/.test(pathname)) {
    return jsonResponse(options.simDetail ?? familySimDetail);
  }

  if (method === "GET" && pathname === "/api/family/relationships") {
    return jsonResponse(options.relationships ?? familyRelationshipsPage);
  }

  if (method === "GET" && pathname === "/api/family/attraction") {
    return jsonResponse(options.attraction ?? familyAttraction);
  }

  if (method === "GET" && pathname === "/api/family/pregnancies") {
    return jsonResponse(options.pregnancies ?? familyPregnanciesPage);
  }

  if (method === "GET" && pathname === "/api/family/children") {
    return jsonResponse(options.children ?? familyChildrenPage);
  }

  if (method === "GET" && pathname === "/api/family/guardianships") {
    return jsonResponse(options.guardianships ?? familyGuardianships);
  }

  if (
    method === "POST" &&
    (pathname === "/api/family/partnerships/force" ||
      pathname === "/api/family/partnerships/end" ||
      pathname === "/api/family/pregnancies" ||
      pathname === "/api/family/guardianships")
  ) {
    return { status: 202, body: "" };
  }

  if (method === "POST" && /^\/api\/family\/pregnancies\/\d+\/cancel$/.test(pathname)) {
    return { status: 202, body: "" };
  }

  if (method === "POST" && /^\/api\/family\/trusts\/\d+\/release$/.test(pathname)) {
    return { status: 202, body: "" };
  }

  return null;
}

function jsonResponse(body: unknown, status = 200): FulfillOptions {
  return {
    status,
    contentType: "application/json",
    body: JSON.stringify(body)
  };
}
