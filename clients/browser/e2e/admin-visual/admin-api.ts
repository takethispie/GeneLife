import type { Page, Request, Route } from "@playwright/test";
import {
  appearancePage,
  consumptionHouseholds,
  consumptionOverview,
  consumptionStores,
  economyFiscal,
  economyHistory,
  economyLedger,
  economyOverview,
  familyChildrenPage,
  familyGenosesPage,
  familyGuardianships,
  familyOverview,
  familyPregnanciesPage,
  familyRelationshipsPage,
  housingConstruction,
  housingHistory,
  housingHouseholds,
  housingOverview,
  housingProperties,
  housingRentRoll,
  lifecycleGenoses,
  lifecycleOverview,
  mapStatus,
  playerLives,
  playerOverview,
  simulationStatus,
  socialOverview,
  worldData
} from "./fixtures";

/**
 * Deterministic admin API mock for the visual-regression baseline.
 *
 * Mirrors the shape of `e2e/admin/family-api.ts`: it rewrites `appsettings*.json`
 * so the Blazor WASM client points its HttpClient at the page origin (where these
 * routes intercept), answers every admin endpoint the pages call from frozen
 * fixtures, and records any call it could not answer. A spec asserts `unhandled`
 * is empty, so a new endpoint added to a page fails the baseline loudly instead
 * of silently rendering an error alert into a screenshot.
 */

export interface ApiCall {
  method: string;
  pathname: string;
  search: string;
  body: unknown;
}

export interface AdminApiHandle {
  calls: ApiCall[];
  unhandled: ApiCall[];
}

type FulfillOptions = Parameters<Route["fulfill"]>[0];

export async function mockAdminApi(page: Page): Promise<AdminApiHandle> {
  const calls: ApiCall[] = [];
  const unhandled: ApiCall[] = [];

  await page.route("**/appsettings*.json", async (route) => {
    // A fixed API origin keeps the URL that the Data page prints ("API <url>")
    // identical regardless of which port the admin host happens to run on, so
    // the baseline is not tied to a developer's chosen port. The `/api/**`
    // route below intercepts this origin too — Playwright matching is by URL
    // pattern, so no DNS entry or live API is required.
    await route.fulfill({ json: { AdminPlayerId: "admin-ui", ApiBaseUrl: "http://admin-api.test" } });
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

    const response = respond(call);
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

function readBody(request: Request): unknown {
  try {
    return request.postDataJSON();
  } catch {
    return request.postData();
  }
}

function respond(call: ApiCall): FulfillOptions | null {
  const { method, pathname } = call;

  if (method === "OPTIONS") return { status: 204 };

  if (method === "POST" && pathname === "/api/auth/local-admin") {
    return json({ accessToken: "e2e-admin-token", expiresAt: "2099-01-01T00:00:00+00:00" });
  }

  if (method !== "GET") return null;

  switch (pathname) {
    case "/api/data":
      return json(worldData);
    case "/api/map":
      return json(mapStatus);
    case "/api/simulation/status":
      return json(simulationStatus);
    case "/api/economy/overview":
      return json(economyOverview);
    case "/api/economy/fiscal":
      return json(economyFiscal);
    case "/api/economy/metrics/history":
      return json(economyHistory);
    case "/api/economy/ledger":
      return json(economyLedger);
    case "/api/housing/overview":
      return json(housingOverview);
    case "/api/housing/properties":
      return json(housingProperties);
    case "/api/housing/households":
      return json(housingHouseholds);
    case "/api/housing/rent-roll":
      return json(housingRentRoll);
    case "/api/housing/construction":
      return json(housingConstruction);
    case "/api/housing/metrics/history":
      return json(housingHistory);
    case "/api/lifecycle/overview":
      return json(lifecycleOverview);
    case "/api/lifecycle/genoses":
      return json(lifecycleGenoses);
    case "/api/family":
      return json(familyOverview);
    case "/api/family/genoses":
      return json(familyGenosesPage);
    case "/api/family/relationships":
      return json(familyRelationshipsPage);
    case "/api/family/pregnancies":
      return json(familyPregnanciesPage);
    case "/api/family/children":
      return json(familyChildrenPage);
    case "/api/family/guardianships":
      return json(familyGuardianships);
    case "/api/appearance":
      return json(appearancePage);
    case "/api/player":
      return json(playerOverview);
    case "/api/player/lives":
      return json(playerLives);
    case "/api/consumption":
      return json(consumptionOverview);
    case "/api/consumption/households":
      return json(consumptionHouseholds);
    case "/api/consumption/stores":
      return json(consumptionStores);
    case "/api/social/admin/overview":
      return json(socialOverview);
    default:
      return null;
  }
}

function json(body: unknown, status = 200): FulfillOptions {
  return { status, contentType: "application/json", body: JSON.stringify(body) };
}
