import { expect, test as base, type Page } from "@playwright/test";

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

const live = process.env.E2E_ADMIN_LIVE === "1";

async function openPage(page: Page, path: string, heading: string): Promise<void> {
  await page.goto(path);
  await expect(page.getByRole("heading", { name: heading, level: 1 })).toBeVisible({
    timeout: 30_000
  });
  await expect(page.locator(".alert.alert-danger")).toHaveCount(0);
}

test.describe("V5 admin release smoke", () => {
  test.skip(!live, "Set E2E_ADMIN_LIVE=1 with the API and seeded preview running");

  test("player, appearance, consumption, map, social and family pages load", async ({ page }) => {
    await openPage(page, "/player", "Players");
    await openPage(page, "/appearance", "Appearance");
    await openPage(page, "/consumption", "Consumption");
    await openPage(page, "/map", "Map");
    await openPage(page, "/social", "Social");
    await openPage(page, "/family", "Family");
  });
});
