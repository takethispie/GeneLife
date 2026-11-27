import { defineConfig, devices } from "@playwright/test";
import path from "node:path";
import { fileURLToPath } from "node:url";

const configDir = path.dirname(fileURLToPath(import.meta.url));
const repositoryRoot = path.resolve(configDir, "../..");
const serverUrl = process.env.E2E_ADMIN_SERVER_URL ?? "http://localhost:5068";
const baseURL = process.env.E2E_ADMIN_BASE_URL ?? serverUrl;
const startServer = process.env.E2E_ADMIN_START_SERVER !== "0";

export default defineConfig({
  testDir: "./e2e/admin",
  outputDir: "test-results-admin",
  fullyParallel: false,
  timeout: 60_000,
  expect: { timeout: 15_000 },
  retries: process.env.CI ? 1 : 0,
  workers: 1,
  reporter: [
    ["list"],
    ["html", { open: "never", outputFolder: "playwright-report-admin" }]
  ],
  use: {
    baseURL,
    trace: "retain-on-failure",
    screenshot: "only-on-failure",
    video: "retain-on-failure"
  },
  projects: [
    {
      name: "admin-chromium",
      use: { ...devices["Desktop Chrome"] }
    },
    // The admin UI switches the sidebar to a fixed 250px column at 641px and
    // keeps it in the row layout until the content fits. Desktop Chrome alone
    // never exercised that band, so a horizontal overflow shipped there (#81).
    // These two projects run only the responsive spec — the interaction specs
    // stay on the desktop project, where their controls are laid out as
    // designed.
    {
      name: "admin-tablet",
      use: { ...devices["Desktop Chrome"], viewport: { width: 768, height: 1024 } },
      testMatch: /responsive\.spec\.ts/
    },
    {
      name: "admin-phone",
      use: { ...devices["Desktop Chrome"], viewport: { width: 375, height: 667 } },
      testMatch: /responsive\.spec\.ts/
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
