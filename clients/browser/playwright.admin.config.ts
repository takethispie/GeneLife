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
