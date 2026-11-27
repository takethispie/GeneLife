import type { Page } from "@playwright/test";

/**
 * The admin E2E suite runs without the API (CI starts only Genelife.Web), so
 * every spec stubs the endpoints it touches. The responsive spec covers all
 * admin routes, so it needs a stub that keeps each page in its normal,
 * data-shaped empty state instead of its "could not load" error state.
 *
 * Two things matter for a layout audit:
 *
 *  - Every response must be the *shape* the page expects. A list endpoint
 *    answered with `{}` throws in the WASM client, renders an error alert and
 *    makes the measurement meaningless.
 *  - Card grids must still render. The responsive regression this suite guards
 *    (`main` refusing to shrink below its content) only shows up when a page
 *    lays out its cards, so the object endpoints return `{}` rather than
 *    failing: the pages bind zero-valued DTOs and paint their cards.
 */
const listPaths = new Set([
  "/api/economy/metrics/history",
  "/api/economy/ledger",
  "/api/housing/properties",
  "/api/housing/households",
  "/api/housing/rent-roll",
  "/api/housing/construction",
  "/api/housing/metrics/history",
  "/api/lifecycle/genoses",
  "/api/family/guardianships"
]);

export async function stubAdminApi(page: Page): Promise<void> {
  await page.route("**/appsettings*.json", async (route) => {
    const origin = new URL(route.request().url()).origin;
    await route.fulfill({
      json: { AdminPlayerId: "admin-ui", ApiBaseUrl: origin }
    });
  });

  await page.route("**/api/**", async (route) => {
    const request = route.request();
    if (request.method() === "OPTIONS") {
      await route.fulfill({ status: 204 });
      return;
    }

    const pathname = new URL(request.url()).pathname;

    if (pathname === "/api/auth/local-admin") {
      await route.fulfill({
        json: {
          accessToken: "e2e-admin-token",
          tokenType: "Bearer",
          expiresAt: "2099-01-01T00:00:00+00:00"
        }
      });
      return;
    }

    // List endpoints are matched by prefix so paginated calls
    // (`/api/appearance?take=…`) hit the array branch as well.
    const isList = [...listPaths].some(
      (path) => pathname === path || pathname.startsWith(`${path}/`)
    );

    await route.fulfill({ json: isList ? [] : {} });
  });
}
