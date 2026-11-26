import { defineConfig, devices } from "@playwright/test";

/**
 * Visual-regression config for the V5 feature 02 appearance acceptance check.
 *
 * Separate from `playwright.config.ts` so that screenshot baselines are not
 * captured or compared during ordinary functional runs. Everything here runs in
 * fixture mode (`?fixtures=1&fixtureSet=appearance`), so only the Vite dev server
 * is needed — no .NET API, no Aspire host.
 *
 *   npm run test:visual              # compare against baselines
 *   npm run test:visual:update       # regenerate baselines after an intended change
 */
const baseURL = process.env.E2E_VISUAL_BASE_URL ?? "http://127.0.0.1:5173";

export default defineConfig({
  testDir: "./e2e/visual",
  outputDir: "test-results-visual",
  fullyParallel: false,
  timeout: 60_000,
  expect: {
    timeout: 20_000,
    toHaveScreenshot: {
      // The scene is static in fixture mode, but GPU rasterisation still differs
      // by a few sub-pixel values between machines and drivers.
      maxDiffPixelRatio: 0.01,
      animations: "disabled"
    }
  },
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report-visual" }]
  ],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    // Video recording is off: it is not useful for screenshot diffs and its
    // encoder keeps the worker process alive after the run finishes.
    video: "off"
  },
  projects: [
    {
      name: "visual-chromium",
      use: {
        ...devices["Desktop Chrome"],
        viewport: { width: 1280, height: 720 },
        deviceScaleFactor: 1
      }
    }
  ],
  // Mirrors playwright.config.ts: the dev server is only started by Playwright
  // when E2E_START_DEV=1. Always-on webServer management hangs the worker on
  // Windows teardown, because killing the `npm run dev` shell leaves the Vite
  // node process alive and Playwright waits on the process tree.
  webServer:
    process.env.E2E_START_DEV === "1"
      ? {
          command: "npm run dev -- --host 127.0.0.1",
          url: "http://127.0.0.1:5173",
          reuseExistingServer: !process.env.CI,
          timeout: 120_000
        }
      : undefined
});
