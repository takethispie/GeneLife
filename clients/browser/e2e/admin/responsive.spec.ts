import { expect, test, type Page } from "@playwright/test";
import { stubAdminApi } from "./responsive-api";

/**
 * Responsive-viewport coverage for the admin WebUI.
 *
 * The suite used to run `admin-chromium` (Desktop Chrome) only, so the whole
 * 641–832px band — where the sidebar switches to a fixed 250px column and the
 * content area has to yield — was never exercised. A ~180px-wide horizontal
 * overflow shipped there unnoticed (#81). These tests run the same page sweep
 * on tablet and phone projects and fail on document-level horizontal overflow.
 *
 * Two distinct regressions are guarded:
 *
 *  - R1 "main refuses to shrink": with `main { flex: 1 }` and no
 *    `min-width: 0`, a flex item's `min-width: auto` resolves to its content's
 *    min-content width, so card grids push the document wider than the
 *    viewport. Any page laying out cards catches it.
 *  - R2 "clipped action": a non-wrapping row with a fixed min-width pushes a
 *    button past the right edge. The clipped-control check catches it.
 */

interface RouteSpec {
  path: string;
  heading: string;
  level?: 1 | 2 | 3;
}

const routes: RouteSpec[] = [
  { path: "/", heading: "GeneLife Admin" },
  { path: "/data", heading: "Data" },
  { path: "/map", heading: "Map" },
  { path: "/economy", heading: "Economy" },
  { path: "/housing", heading: "Housing" },
  { path: "/lifecycle", heading: "Life Stages" },
  { path: "/family", heading: "Family" },
  { path: "/players", heading: "Players" },
  { path: "/appearance", heading: "Appearance" },
  { path: "/consumption", heading: "Consumption" },
  { path: "/social", heading: "Social" },
  { path: "/not-found", heading: "Not Found", level: 3 }
];

interface LayoutMetrics {
  scrollWidth: number;
  clientWidth: number;
  mainRight: number;
  clippedControls: string[];
}

async function measureLayout(page: Page): Promise<LayoutMetrics> {
  return page.evaluate(() => {
    const doc = document.documentElement;
    const clientWidth = doc.clientWidth;
    const main = document.querySelector("main");
    const mainRect = main?.getBoundingClientRect();

    const clippedControls: string[] = [];
    const controls = Array.from(
      document.querySelectorAll<HTMLElement>("main a, main button, main input, main select, main textarea")
    );

    for (const control of controls) {
      const rect = control.getBoundingClientRect();
      if (rect.width === 0 && rect.height === 0) continue;

      // Controls inside a horizontal scroll container may legitimately sit
      // past the viewport edge — the container scrolls to them. Only flag
      // controls that no scroll container protects.
      let scrollable = false;
      for (let node = control.parentElement; node; node = node.parentElement) {
        const overflowX = getComputedStyle(node).overflowX;
        if (overflowX === "auto" || overflowX === "scroll") {
          scrollable = true;
          break;
        }
      }
      if (scrollable) continue;

      if (rect.right > clientWidth + 1 || rect.left < -1) {
        const label =
          control.getAttribute("aria-label") ??
          control.textContent?.trim().slice(0, 40) ??
          control.tagName.toLowerCase();
        clippedControls.push(`${control.tagName.toLowerCase()} "${label}" right=${Math.round(rect.right)}`);
      }
    }

    return {
      scrollWidth: doc.scrollWidth,
      clientWidth,
      mainRight: mainRect ? Math.round(mainRect.right) : -1,
      clippedControls
    };
  });
}

async function openRoute(page: Page, route: RouteSpec): Promise<void> {
  await page.goto(route.path);
  await expect(
    page.getByRole("heading", { name: route.heading, level: route.level ?? 1 })
  ).toBeVisible();
  // Let the WASM render settle after the first paint so the measured layout is
  // the final one (cards/images/table wrappers all in place).
  await page.waitForTimeout(150);
}

test.describe("admin responsive layout", () => {
  test.beforeEach(async ({ page }) => {
    await stubAdminApi(page);
  });

  for (const route of routes) {
    test(`${route.path} fits the viewport without horizontal overflow`, async ({ page }) => {
      await openRoute(page, route);

      // Empty-shaped stubs must render each page's normal empty state, not an
      // error alert: an error page hides the layout we are auditing.
      await expect(page.locator(".alert-danger")).toHaveCount(0);

      const metrics = await measureLayout(page);
      expect(
        metrics.scrollWidth,
        `${route.path} overflows horizontally at ${metrics.clientWidth}px`
      ).toBeLessThanOrEqual(metrics.clientWidth + 1);
      expect(metrics.mainRight, `${route.path} main is pushed past the viewport`).toBeLessThanOrEqual(
        metrics.clientWidth + 1
      );
      expect(metrics.clippedControls, `${route.path} clips controls at ${metrics.clientWidth}px`).toEqual([]);
    });
  }

  test("Appearance keeps its search action on screen at 360px", async ({ page }) => {
    await page.setViewportSize({ width: 360, height: 640 });
    await openRoute(page, { path: "/appearance", heading: "Appearance" });

    const search = page.getByRole("textbox", { name: "Search genos name or network id" });
    const button = page.getByRole("button", { name: "Search" });
    await expect(search).toBeVisible();
    await expect(button).toBeVisible();

    const metrics = await measureLayout(page);
    expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
    expect(metrics.clippedControls).toEqual([]);

    const buttonRight = await button.evaluate((element) => element.getBoundingClientRect().right);
    expect(Math.round(buttonRight)).toBeLessThanOrEqual(metrics.clientWidth);
  });

  test("the collapsible navigation stays usable at phone width", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "admin-phone", "phone-only navigation behaviour");

    await openRoute(page, { path: "/", heading: "GeneLife Admin" });
    const toggler = page.getByRole("button", { name: "Navigation menu" });
    await expect(toggler).toBeVisible();
    await expect(toggler).toHaveAttribute("aria-expanded", "false");

    await toggler.click();
    await expect(toggler).toHaveAttribute("aria-expanded", "true");
    // Scope to the collapsible region: the dashboard cards also link to Economy.
    await expect(page.locator("#nav-menu").getByRole("link", { name: "Economy" })).toBeVisible();

    const metrics = await measureLayout(page);
    expect(metrics.scrollWidth).toBeLessThanOrEqual(metrics.clientWidth + 1);
    expect(metrics.clippedControls).toEqual([]);
  });

  test("card-heavy pages stay clean across the 641–832px band", async ({ page }, testInfo) => {
    test.skip(testInfo.project.name !== "admin-tablet", "tablet-only band sweep");

    const band = [641, 700, 768, 820];
    const bandRoutes = ["/", "/economy", "/housing", "/family"];

    for (const width of band) {
      await page.setViewportSize({ width, height: 900 });
      for (const path of bandRoutes) {
        await openRoute(page, routes.find((route) => route.path === path)!);
        const metrics = await measureLayout(page);
        expect(
          metrics.scrollWidth,
          `${path} overflows horizontally at ${width}px`
        ).toBeLessThanOrEqual(metrics.clientWidth + 1);
      }
    }
  });
});
