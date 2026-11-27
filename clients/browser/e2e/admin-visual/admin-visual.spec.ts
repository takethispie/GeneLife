import { expect, test as base, type Page } from "@playwright/test";
import { mockAdminApi } from "./admin-api";

/**
 * Admin visual-regression baseline for `Genelife.Web`.
 *
 * Every admin route is rendered against a frozen API mock and captured as a
 * full-page screenshot. The baseline exists because 35 green component tests
 * still let a stale Dashboard stub and a broken F1 surface reach the running
 * UI: nothing ever rendered a page and compared it to a known-good image.
 *
 * Run:
 *   npm run test:visual:admin          # compare against the committed baselines
 *   npm run test:visual:admin:update   # regenerate after an intended change
 *
 * The baselines are Windows/Chromium specific (the file name carries `-win32`);
 * like the game-client visual suite they are a local acceptance tool, not a CI
 * gate. The `loads without placeholder copy` guards below are the platform
 * independent part and fail on the exact defect class this card was opened for.
 */

const test = base.extend<{ pageErrors: string[] }>({
  pageErrors: async ({ page }, use) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await use(errors);
  }
});

interface Route {
  /** Human label used in the test title and snapshot name. */
  name: string;
  path: string;
  /** Visible heading text (any level). */
  heading: string;
  /** Expected `document.title`. */
  title: string;
}

const ROUTES: Route[] = [
  { name: "home", path: "/", heading: "GeneLife Admin", title: "GeneLife Admin" },
  { name: "data", path: "/data", heading: "Data", title: "Data" },
  { name: "map", path: "/map", heading: "Map", title: "Map" },
  { name: "economy", path: "/economy", heading: "Economy", title: "Economy" },
  { name: "housing", path: "/housing", heading: "Housing", title: "Housing" },
  { name: "lifecycle", path: "/lifecycle", heading: "Life Stages", title: "Life Stages" },
  { name: "family", path: "/family", heading: "Family", title: "Family" },
  { name: "players", path: "/players", heading: "Players", title: "Players" },
  { name: "appearance", path: "/appearance", heading: "Appearance", title: "Appearance" },
  { name: "consumption", path: "/consumption", heading: "Consumption", title: "Consumption" },
  { name: "social", path: "/social", heading: "Social", title: "Social" },
  { name: "not-found", path: "/not-found", heading: "Not Found", title: "GeneLife Admin" }
];

/** Copy that must never be visible on an admin page again. */
const FORBIDDEN_COPY = ["Barebone", "not implemented", "features are not implemented"];

async function openAdminPage(page: Page, route: Route): Promise<void> {
  await page.goto(route.path);
  await expect(page.getByRole("heading", { name: route.heading }).first()).toBeVisible({
    timeout: 30_000
  });
  // Settle: wait for async data to land (no spinner and real body copy in the
  // page article) before measuring, so the baseline is not captured mid-load.
  await page.waitForFunction(() => {
    const article = document.querySelector("article");
    if (!article) return false;
    if (article.querySelector(".spinner-border")) return false;
    return (article.textContent ?? "").trim().length > 30;
  });
  await page.waitForLoadState("networkidle");
}

test.afterEach(({ pageErrors }) => {
  expect(pageErrors, "the page must not raise uncaught JS errors").toEqual([]);
});

test.describe("admin page content guards", () => {
  for (const route of ROUTES) {
    test(`${route.name} loads without placeholder copy`, async ({ page }) => {
      const api = await mockAdminApi(page);
      await openAdminPage(page, route);

      await expect(page).toHaveTitle(route.title);
      await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

      const article = page.locator("article");
      for (const copy of FORBIDDEN_COPY) {
        await expect(article).not.toContainText(copy);
      }

      expect(
        api.unhandled,
        `unmocked API calls on ${route.path}: ${api.unhandled.map((c) => c.pathname).join(", ")}`
      ).toEqual([]);
    });
  }
});

test.describe("admin visual baseline", () => {
  for (const route of ROUTES) {
    test(`${route.name} matches its screenshot baseline`, async ({ page }) => {
      await mockAdminApi(page);
      await openAdminPage(page, route);

      await expect(page).toHaveScreenshot(`${route.name}.png`, {
        fullPage: true,
        // The header prints the wall-clock load time ("Refreshed 09:19:04"); it
        // is a real time that changes every run, so mask it rather than let it
        // poison the diff. The API origin is pinned by the mock, so nothing else
        // in the header is run dependent.
        mask: [page.getByText(/^Refreshed \d{2}:\d{2}:\d{2}$/)]
      });
    });
  }
});
