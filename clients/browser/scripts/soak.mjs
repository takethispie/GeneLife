#!/usr/bin/env node
/**
 * Long-running smoke/soak check for the browser client.
 *
 *   npm run soak -- --minutes=30 --url="http://127.0.0.1:5173/?playerId=soak"
 *
 * Requires the API (with a running simulation) and the Vite dev server. Fails on
 * page errors, console errors, a non-connected HUD, or a tick that stops moving
 * for three consecutive checks.
 */

import { chromium } from "@playwright/test";

const args = new Map(
  process.argv.slice(2).map((arg) => {
    const [key, value = "true"] = arg.replace(/^--/, "").split("=");
    return [key, value];
  })
);

const minutes = Number(args.get("minutes") ?? process.env.SOAK_MINUTES ?? "5");
const url =
  args.get("url") ??
  process.env.SOAK_URL ??
  "http://127.0.0.1:5173/?playerId=soak-" + Math.random().toString(36).slice(2, 8);
const intervalMs = Number(args.get("interval-ms") ?? process.env.SOAK_INTERVAL_MS ?? "10000");
const durationMs = Math.max(10_000, minutes * 60_000);

const errors = [];
const browser = await chromium.launch();
const context = await browser.newContext({ viewport: { width: 1280, height: 720 } });
const page = await context.newPage();

page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
page.on("console", (message) => {
  if (message.type() === "error") errors.push(`console: ${message.text()}`);
});

console.log(`soak: ${url} for ${minutes} minute(s)`);
await page.goto(url);
await page.waitForSelector("#loading", { state: "hidden", timeout: 30_000 });

let lastTick = 0;
let stalled = 0;
let checks = 0;
let maxFps = 0;
let minFps = Number.POSITIVE_INFINITY;
const started = Date.now();

while (Date.now() - started < durationMs) {
  await page.waitForTimeout(intervalMs);
  checks++;
  const connected = await page.locator('[data-field="connection"]').textContent();
  const tick = Number(await page.locator('[data-field="tick"]').textContent());
  const fps = Number(await page.locator('[data-field="fps"]').textContent());

  if (connected !== "Connected") {
    errors.push(`connection state: ${connected}`);
  }
  if (tick > lastTick) {
    lastTick = tick;
    stalled = 0;
  } else {
    stalled++;
    if (stalled >= 3) errors.push(`tick stalled at ${tick}`);
  }
  if (Number.isFinite(fps) && fps > 0) {
    maxFps = Math.max(maxFps, fps);
    minFps = Math.min(minFps, fps);
  }
  console.log(
    `check ${checks}: ${connected} tick=${tick} fps=${Number.isFinite(fps) ? fps.toFixed(0) : "?"} errors=${errors.length}`
  );
}

const summary = {
  url,
  minutes,
  checks,
  lastTick,
  fpsMin: Number.isFinite(minFps) ? minFps : null,
  fpsMax: Number.isFinite(maxFps) ? maxFps : null,
  errors
};
console.log(JSON.stringify(summary, null, 2));

await context.close();
await browser.close();

if (errors.length > 0) {
  console.error(`soak FAILED with ${errors.length} error(s)`);
  process.exit(1);
}
console.log("soak PASSED");
