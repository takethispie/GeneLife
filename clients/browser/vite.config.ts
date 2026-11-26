import { defineConfig } from "vitest/config";

const buildStamp =
  process.env.VITE_BUILD_STAMP ??
  process.env.GITHUB_SHA?.slice(0, 12) ??
  new Date().toISOString();

export default defineConfig({
  define: {
    __BUILD_STAMP__: JSON.stringify(buildStamp)
  },
  server: {
    port: 5173,
    strictPort: true,
    watch: {
      // Playwright writes traces/screenshots into these directories while the
      // dev server is running. Without the wildcards a new output directory
      // (test-results-visual, playwright-report-admin, ...) is watched, and the
      // resulting HMR reloads reset the page mid-test. Each pattern is listed
      // twice: the bare form ignores the directory itself, the /** form its
      // contents — ignoring only the contents still leaves a watcher open on
      // the directory, which then blocks Playwright from recreating it.
      ignored: [
        "**/coverage",
        "**/coverage/**",
        "**/dist",
        "**/dist/**",
        "**/playwright-report*",
        "**/playwright-report*/**",
        "**/test-results*",
        "**/test-results*/**"
      ]
    }
  },
  preview: {
    port: 4173,
    strictPort: true
  },
  build: {
    target: "es2022",
    sourcemap: process.env.VITE_SOURCEMAPS === "true",
    assetsInlineLimit: 0
  },
  test: {
    environment: "jsdom",
    include: ["src/**/*.test.ts"],
    exclude: ["e2e/**", "node_modules/**", "dist/**"],
    coverage: {
      provider: "v8",
      reporter: ["text", "lcov"],
      include: [
        "src/simulation/**/*.ts",
        "src/net/intent-client.ts",
        "src/net/viewport-client.ts",
        "src/net/recovery.ts",
        "src/input/targeting.ts",
        "src/input/input-map.ts",
        "src/ui/ui-store.ts",
        "src/ui/notifications.ts",
        "src/ui/hud.ts",
        "src/ui/settings.ts",
        "src/render/map/tile-visual-cache.ts",
        "src/contracts/validate.ts",
        "src/config/env.ts",
        "src/auth/session.ts"
      ],
      exclude: ["**/*.test.ts"],
      thresholds: {
        lines: 55,
        functions: 55,
        statements: 55,
        branches: 45
      }
    }
  }
});
