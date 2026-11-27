import { defineConfig, devices } from "@playwright/test";
import path from "node:path";
import { fileURLToPath } from "node:url";

/**
 * Visual-regression config for the admin (`Genelife.Web`) Blazor WASM app.
 *
 * Every admin route is rendered against a deterministic API mock and compared to
 * a committed full-page screenshot. This is the only tier that renders a whole
 * page and diffs it against a known-good image — component (bunit) and functional
 * admin specs both stayed green while the Dashboard still showed its old stub.
 *
 *   npm run test:visual:admin          # compare against baselines
 *   npm run test:visual:admin:update   # regenerate after an intended change
 *
 * Separated from `playwright.admin.config.ts` so functional runs neither capture
 * nor compare screenshots.
 */
const configDir = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(configDir, "../..");
const serverUrl = process.env.E2E_ADMIN_SERVER_URL ?? "http://localhost:5068";
const baseURL = process.env.E2E_ADMIN_BASE_URL ?? serverUrl;
const startServer = process.env.E2E_ADMIN_START_SERVER !== "0";

export default defineConfig({
  testDir: "./e2e/admin-visual",
  outputDir: "test-results-admin-visual",
  snapshotPathTemplate: "{testDir}/__screenshots__/{arg}{-projectName}{-snapshotSuffix}{ext}",
  fullyParallel: false,
  timeout: 60_000,
  expect: {
    timeout: 15_000,
    toHaveScreenshot: {
      // Chromium rendering on one machine is reproducible, so the pixel budget
      // stays tiny: a couple of hundred anti-aliasing pixels of drift is normal,
      // but any real copy/layout change moves far more. A loose 1% budget once
      // let a changed header URL slip through unnoticed.
      maxDiffPixels: 200,
      animations: "disabled"
    }
  },
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report-admin-visual" }]
  ],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "off"
  },
  projects: [
    {
      name: "admin-visual-chromium",
      use: {
        ...devices["Desktop Chrome"],
        // Wide enough for the desktop sidebar + multi-column cards, fixed so the
        // baseline is viewport independent of the developer's screen.
        viewport: { width: 1440, height: 900 },
        deviceScaleFactor: 1
      }
    }
  ],
  webServer: startServer
    ? {
        command: "dotnet run --project Genelife.Web -c Release --no-launch-profile",
        cwd: repositoryRoot,
        env: {
          ASPNETCORE_ENVIRONMENT: "Development",
          ASPNETCORE_URLS: serverUrl
        },
        url: serverUrl,
        reuseExistingServer: !process.env.CI,
        timeout: 240_000
      }
    : undefined
});
