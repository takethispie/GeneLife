#!/usr/bin/env node
/**
 * Reference-hardware frame-budget report for the browser client.
 *
 *   npm run frame-budget
 *   npm run frame-budget -- --seconds=60 --budget-ms=16.7
 *   npm run frame-budget -- --headless --out=performance-baselines/ci-frame-budget.json
 *
 * This is the "reference-hardware frame-budget report" required by
 * docs/plans/release-roadmap-V5 (feature 02 appearance acceptance). It loads the
 * `stress` fixture set — 2,000 genoses WITH appearance data, which no fixture
 * carried before — and samples the render loop's own frame-time counter.
 *
 * Runs HEADED by default so the measurement reflects a real GPU. Headless
 * Chromium falls back to SwiftShader (software rasterisation) and reports frame
 * times that say nothing about reference hardware; the report records which
 * renderer was actually used so the numbers are never misread.
 *
 * Requires the Vite dev server (npm run dev) and, for the fixture set, no .NET
 * backend at all.
 */

import { mkdirSync, writeFileSync } from "node:fs";
import { dirname, join, resolve } from "node:path";
import { chromium } from "@playwright/test";

const args = new Map(
  process.argv.slice(2).map((arg) => {
    const [key, value = "true"] = arg.replace(/^--/, "").split("=");
    return [key, value];
  })
);

const seconds = Number(args.get("seconds") ?? process.env.FRAME_BUDGET_SECONDS ?? "30");
const budgetMs = Number(args.get("budget-ms") ?? process.env.FRAME_BUDGET_MS ?? "16.7");
const warmupMs = Number(args.get("warmup-ms") ?? "3000");
const sampleIntervalMs = Number(args.get("sample-interval-ms") ?? "100");
const headless = args.get("headless") === "true" || process.env.FRAME_BUDGET_HEADLESS === "1";
const fixtureSet = args.get("fixture-set") ?? "stress";
const baseUrl = args.get("url") ?? process.env.FRAME_BUDGET_URL ?? "http://127.0.0.1:5173";
const outPath = resolve(
  args.get("out") ?? "performance-baselines/browser-frame-budget.json"
);

const url = `${baseUrl}/?fixtures=1&fixtureSet=${fixtureSet}&playerId=frame-budget`;
const durationMs = Math.max(5_000, seconds * 1_000);

function percentile(sorted, fraction) {
  if (sorted.length === 0) return 0;
  const index = Math.min(sorted.length - 1, Math.floor(sorted.length * fraction));
  return sorted[index];
}

const errors = [];
const failedRequests = [];
const browser = await chromium.launch({ headless });
const context = await browser.newContext({
  viewport: { width: 1280, height: 720 },
  deviceScaleFactor: 1
});
const page = await context.newPage();

page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
page.on("console", (message) => {
  if (message.type() === "error") errors.push(`console: ${message.text()}`);
});
page.on("response", (response) => {
  if (response.status() >= 400) failedRequests.push(`${response.status()} ${response.url()}`);
});

console.log(`frame-budget: ${url}`);
console.log(`  ${seconds}s sample, ${budgetMs}ms budget, headless=${headless}`);

await page.goto(url);
await page.waitForSelector("#loading", { state: "hidden", timeout: 30_000 });
await page.waitForSelector("#app canvas", { state: "visible", timeout: 30_000 });

// The debug overlay carries the render loop's own fps/frame-time counters and
// the renderer string; the HUD's Performance panel is not always expanded.
await page.keyboard.press("F3");
const overlay = page.locator(".debug-overlay");
await overlay.waitFor({ state: "visible", timeout: 10_000 });

const genosField = await page.locator('#hud [data-field="genoses"]').textContent();
console.log(`  genoses in scene: ${genosField}`);

// Let the scene warm up: shader compilation and texture upload land in the first
// frames and would otherwise dominate the percentiles.
await page.waitForTimeout(warmupMs);

const frameTimes = [];
const fpsSamples = [];
let renderer = "";
let visibleGenoses = 0;
const started = Date.now();

while (Date.now() - started < durationMs) {
  const text = (await overlay.textContent()) ?? "";
  const frame = /fps\s+([\d.]+)\s+frame\s+([\d.]+)ms/.exec(text);
  if (frame !== null) {
    const fps = Number(frame[1]);
    const frameMs = Number(frame[2]);
    if (Number.isFinite(fps) && fps > 0) fpsSamples.push(fps);
    if (Number.isFinite(frameMs) && frameMs > 0) frameTimes.push(frameMs);
  }
  if (renderer === "") {
    const match = /renderer\s+(\S+)/.exec(text);
    if (match !== null && match[1] !== "-") renderer = match[1];
  }
  const visible = /genoses\s+(\d+)\//.exec(text);
  if (visible !== null) visibleGenoses = Math.max(visibleGenoses, Number(visible[1]));

  await page.waitForTimeout(sampleIntervalMs);
}

await browser.close();

const sorted = [...frameTimes].sort((a, b) => a - b);
const p50 = percentile(sorted, 0.5);
const p95 = percentile(sorted, 0.95);
const p99 = percentile(sorted, 0.99);

const report = {
  generatedAt: new Date().toISOString(),
  fixtureSet,
  genosesInScene: Number((genosField ?? "0/0").split("/")[1] ?? 0),
  genosesVisiblePeak: visibleGenoses,
  viewport: "1280x720",
  deviceScaleFactor: 1,
  headless,
  renderer: renderer || "unknown",
  hardwareAccelerated: !headless,
  durationSeconds: seconds,
  samples: sorted.length,
  frameMs: {
    min: sorted.length > 0 ? sorted[0] : 0,
    p50,
    p95,
    p99,
    max: sorted.length > 0 ? sorted[sorted.length - 1] : 0
  },
  fps: {
    min: fpsSamples.length > 0 ? Math.min(...fpsSamples) : 0,
    mean:
      fpsSamples.length > 0
        ? fpsSamples.reduce((sum, value) => sum + value, 0) / fpsSamples.length
        : 0
  },
  budgetMs,
  verdict: p95 <= budgetMs ? "pass" : "fail",
  consoleErrors: errors,
  failedRequests,
  note:
    "p95 frame time is compared against budgetMs (16.7ms = 60fps). Headless runs use SwiftShader and are NOT reference-hardware numbers."
};

mkdirSync(dirname(outPath), { recursive: true });
writeFileSync(outPath, `${JSON.stringify(report, null, 2)}\n`);

const markdown = [
  "# Browser frame-budget report",
  "",
  `Generated: ${report.generatedAt}`,
  "",
  "| Field | Value |",
  "|---|---|",
  `| Fixture set | \`${fixtureSet}\` (${report.genosesInScene} genoses with appearance) |`,
  `| Genoses visible (peak) | ${report.genosesVisiblePeak} |`,
  `| Viewport | ${report.viewport} @ ${report.deviceScaleFactor}x |`,
  `| Renderer | ${report.renderer}${report.hardwareAccelerated ? "" : " (software — not reference hardware)"} |`,
  `| Samples | ${report.samples} over ${seconds}s |`,
  `| Frame p50 | ${p50.toFixed(2)} ms |`,
  `| Frame p95 | ${p95.toFixed(2)} ms |`,
  `| Frame p99 | ${p99.toFixed(2)} ms |`,
  `| FPS min / mean | ${report.fps.min.toFixed(1)} / ${report.fps.mean.toFixed(1)} |`,
  `| Budget (p95) | ${budgetMs} ms |`,
  `| **Verdict** | **${report.verdict.toUpperCase()}** |`,
  "",
  report.note,
  ""
].join("\n");

const markdownPath = outPath.replace(/\.json$/, ".md");
writeFileSync(markdownPath, markdown);

console.log("");
console.log(markdown);
console.log(`JSON:     ${outPath}`);
console.log(`Markdown: ${markdownPath}`);

if (errors.length > 0) {
  console.error(`console errors during run:\n  ${errors.join("\n  ")}`);
}
if (failedRequests.length > 0) {
  console.error(`failed requests during run:\n  ${failedRequests.join("\n  ")}`);
}

process.exit(report.verdict === "pass" && errors.length === 0 ? 0 : 1);
