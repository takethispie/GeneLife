import { expect, test as base, type Page } from "@playwright/test";
import { mockAdminPagesApi } from "./pages-api";

const test = base.extend<{ pageErrors: string[] }>({
  pageErrors: async ({ page }, use) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    await use(errors);
  }
});

test.afterEach(({ pageErrors }) => {
  expect(pageErrors).toEqual([]);
});

interface AdminRoute {
  path: string;
  heading: string;
  title: string;
  level: number;
}

/**
 * The admin routes without a dedicated spec file. Map, Family and Social keep
 * their own content suites; this file proves the remaining pages load against a
 * mocked API with no unhandled calls and no error alert.
 */
const routes: AdminRoute[] = [
  { path: "/", heading: "GeneLife Admin", title: "GeneLife Admin", level: 1 },
  { path: "/data", heading: "Data", title: "Data", level: 1 },
  { path: "/economy", heading: "Economy", title: "Economy", level: 1 },
  { path: "/housing", heading: "Housing", title: "Housing", level: 1 },
  { path: "/lifecycle", heading: "Life Stages", title: "Life Stages", level: 1 },
  { path: "/players", heading: "Players", title: "Players", level: 1 },
  { path: "/appearance", heading: "Appearance", title: "Appearance", level: 1 },
  { path: "/causes", heading: "Causes", title: "Causes", level: 1 },
  { path: "/consumption", heading: "Consumption", title: "Consumption", level: 1 }
];

async function openRoute(
  page: Page,
  route: AdminRoute,
  api: Awaited<ReturnType<typeof mockAdminPagesApi>>
): Promise<void> {
  await page.goto(route.path);
  await expect(
    page.getByRole("heading", { name: route.heading, level: route.level })
  ).toBeVisible({ timeout: 30_000 });
  await expect(page).toHaveTitle(route.title);
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);
  expect(api.unhandled, `unhandled API calls on ${route.path}`).toEqual([]);
}

for (const route of routes) {
  test(`loads ${route.path} against a mocked API`, async ({ page }) => {
    const api = await mockAdminPagesApi(page);
    await openRoute(page, route, api);
  });
}

const homeRoute: AdminRoute = {
  path: "/",
  heading: "GeneLife Admin",
  title: "GeneLife Admin",
  level: 1
};

test("dashboard links to every admin section", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await openRoute(page, homeRoute, api);

  const links = [
    ["Data", "data"],
    ["Map", "map"],
    ["Economy", "economy"],
    ["Housing", "housing"],
    ["Life Stages", "lifecycle"],
    ["Family", "family"],
    ["Players", "players"],
    ["Appearance", "appearance"],
    ["Causes", "causes"],
    ["Consumption", "consumption"],
    ["Social", "social"]
  ] as const;

  for (const [name, href] of links) {
    const card = page.locator("article.content .card", { hasText: name }).first();
    await expect(card).toHaveAttribute("href", href);
  }
});

test("data page renders summary counts and switches tabs", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await page.goto("/data");
  await expect(page.getByRole("heading", { name: "Data", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

  await expect(page.getByText("Tick 1234")).toBeVisible();
  const summary = page.locator(".row.g-3.mb-3").first();
  await expect(summary.locator(".card", { hasText: "NPCs" })).toContainText("1");
  await expect(summary.locator(".card", { hasText: "Companies" })).toContainText("1");

  await page.getByRole("tab", { name: /Companies/ }).click();
  await expect(page.getByRole("cell", { name: "Acme Manufacturing" })).toBeVisible();
  expect(api.unhandled).toEqual([]);
});

test("economy page renders the money loop from world data", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await page.goto("/economy");
  await expect(page.getByRole("heading", { name: "Economy", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

  await expect(page.getByText("Year 5")).toBeVisible();
  await expect(page.getByText("Money supply 1,185.00")).toBeVisible();
  await expect(page.getByText("Money conservation")).toBeVisible();
  expect(api.unhandled).toEqual([]);
});

test("housing page renders the tenancy overview", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await page.goto("/housing");
  await expect(page.getByRole("heading", { name: "Housing", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

  await expect(page.getByText("Year 5")).toBeVisible();
  await expect(page.getByText("Rent index 1.200")).toBeVisible();
  await expect(page.locator(".card", { hasText: "Units" }).first()).toContainText("9 / 12");
  expect(api.unhandled).toEqual([]);
});

test("life stages page renders the population overview", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await page.goto("/lifecycle");
  await expect(page.getByRole("heading", { name: "Life Stages", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

  await expect(page.getByText("120 genoses")).toBeVisible();
  await expect(page.getByText("Policy 18-65-75")).toBeVisible();
  expect(api.unhandled).toEqual([]);
});

test("players page renders the player totals", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await page.goto("/players");
  await expect(page.getByRole("heading", { name: "Players", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

  await expect(page.getByText("2 active · 1 awaiting")).toBeVisible();
  expect(api.unhandled).toEqual([]);
});

test("appearance page renders the search harness and empty selection", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await page.goto("/appearance");
  await expect(page.getByRole("heading", { name: "Appearance", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

  await expect(
    page.getByRole("textbox", { name: "Search genos name or network id" })
  ).toBeVisible();
  await expect(page.getByText("0 living")).toBeVisible();
  await expect(
    page.getByText("Select a genos to preview its descriptor and layers.")
  ).toBeVisible();
  expect(api.unhandled).toEqual([]);
});

test("consumption page renders utility and grocery totals", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await page.goto("/consumption");
  await expect(page.getByRole("heading", { name: "Consumption", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);

  await expect(page.locator(".card", { hasText: "Households" }).first()).toContainText(
    "1 reduced · 1 cut off"
  );
  await expect(page.getByRole("cell", { name: "Water" })).toBeVisible();
  // NOTE: the grocery card also shows "catalog v@overview.GroceryCatalogVersion" —
  // the Razor `v@` sequence is read as an email literal, so the version renders
  // literally instead of a number. Tracked as a defect; assert the stable part here.
  await expect(page.locator(".card", { hasText: "Grocery" }).first()).toContainText(
    "40 stock units"
  );
  expect(api.unhandled).toEqual([]);
});

test("not-found route renders the fallback page", async ({ page }) => {
  const api = await mockAdminPagesApi(page);
  await page.goto("/not-found");
  await expect(page.getByRole("heading", { name: "Not Found", level: 3 })).toBeVisible({
    timeout: 30_000
  });
  expect(api.unhandled).toEqual([]);
});

test("data page surfaces an API failure as an error alert", async ({ page }) => {
  await mockAdminPagesApi(page, {
    failures: [{ method: "GET", pathname: "/api/data", status: 500 }]
  });
  await page.goto("/data");
  await expect(page.getByRole("heading", { name: "Data", level: 1 })).toBeVisible({
    timeout: 30_000
  });
  const alert = page.locator(".alert.alert-danger");
  await expect(alert).toBeVisible();
  await expect(alert).toContainText("Could not load world data.");
});
