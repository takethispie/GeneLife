import { expect, test, type APIRequestContext } from "@playwright/test";
import { canvasCenter, hudField, liveUrl, openWorld } from "./helpers";

const live = process.env.E2E_LIVE === "1";
const apiBase = process.env.E2E_API_BASE_URL ?? "http://localhost:5288";

async function adminToken(request: APIRequestContext): Promise<string> {
  const auth = await request.post(`${apiBase}/api/auth/local-admin`);
  if (!auth.ok()) throw new Error(`local-admin token failed: ${auth.status()}`);
  const body = (await auth.json()) as { accessToken: string };
  return body.accessToken;
}

async function ensureSimulationRunning(request: APIRequestContext): Promise<void> {
  const auth = await request.post(`${apiBase}/api/auth/local-admin`);
  if (!auth.ok()) {
    throw new Error(`local-admin token failed: ${auth.status()} ${await auth.text()}`);
  }
  const body = (await auth.json()) as { accessToken: string };
  const start = await request.post(`${apiBase}/api/simulation/start`, {
    headers: { Authorization: `Bearer ${body.accessToken}` }
  });
  if (start.status() !== 200 && start.status() !== 400) {
    throw new Error(`start simulation failed: ${start.status()} ${await start.text()}`);
  }
}

test.describe("live stack acceptance", () => {
  test.skip(!live, "Set E2E_LIVE=1 and run the API + Vite to execute these tests");

  test.beforeAll(async ({ request }) => {
    if (!live) return;
    await ensureSimulationRunning(request);
  });

  test("connects, advances ticks and resumes after reload", async ({ page }) => {
    const playerId = `e2e-live-${Date.now()}`;
    await openWorld(page, liveUrl(playerId));

    await expect(hudField(page, "connection")).toHaveText("Connected", { timeout: 20_000 });
    await expect(hudField(page, "player")).toHaveText(playerId);
    await expect(hudField(page, "sim")).not.toHaveText("—");
    await expect(hudField(page, "map")).toHaveText("world");

    const simId = await hudField(page, "sim").textContent();
    await expect
      .poll(async () => Number(await hudField(page, "tick").textContent()), { timeout: 15_000 })
      .toBeGreaterThan(0);

    const center = await canvasCenter(page);
    await page.mouse.click(center.x, center.y);
    await page.waitForTimeout(500);
    await expect(page.locator(".notification-error")).toHaveCount(0);

    await page.reload();
    await expect(page.locator("#loading")).toBeHidden({ timeout: 20_000 });
    await expect(hudField(page, "connection")).toHaveText("Connected", { timeout: 20_000 });
    await expect(hudField(page, "sim")).toHaveText(simId ?? "");
  });

  test("two tabs own different sims in the same world", async ({ page, context }) => {
    const first = `e2e-a-${Date.now()}`;
    const second = `e2e-b-${Date.now()}`;

    await openWorld(page, liveUrl(first));
    await expect(hudField(page, "connection")).toHaveText("Connected", { timeout: 20_000 });
    await expect(hudField(page, "sim")).not.toHaveText("—");

    const other = await context.newPage();
    await openWorld(other, liveUrl(second));
    await expect(hudField(other, "connection")).toHaveText("Connected", { timeout: 20_000 });
    await expect(hudField(other, "sim")).not.toHaveText("—");

    const firstSim = await hudField(page, "sim").textContent();
    const secondSim = await hudField(other, "sim").textContent();
    expect(firstSim).not.toBe(secondSim);

    await other.close();
  });

  test("recovers the connection after a network interruption", async ({ page, context }) => {
    test.slow();
    await openWorld(page, liveUrl(`e2e-reconnect-${Date.now()}`));
    await expect(hudField(page, "connection")).toHaveText("Connected", { timeout: 20_000 });

    await context.setOffline(true);
    await expect(hudField(page, "connection")).not.toHaveText("Connected", { timeout: 50_000 });
    await context.setOffline(false);
    await expect(hudField(page, "connection")).toHaveText("Connected", { timeout: 45_000 });
    await expect
      .poll(async () => Number(await hudField(page, "tick").textContent()), { timeout: 20_000 })
      .toBeGreaterThan(0);
  });
});

test.describe("live V5 release smoke", () => {
  test.skip(!live, "Set E2E_LIVE=1 and run the API + Vite to execute these tests");

  test("player, appearance, consumption and social read models respond", async ({ request }) => {
    const token = await adminToken(request);
    const headers = { Authorization: `Bearer ${token}` };

    for (const path of [
      "/api/player",
      "/api/appearance",
      "/api/consumption",
      "/api/consumption/networks",
      "/api/consumption/stores",
      "/api/social/admin/overview",
      "/api/social/admin/presence",
      "/api/family"
    ]) {
      const response = await request.get(`${apiBase}${path}`, { headers });
      expect(response.ok(), `${path} returned ${response.status()}`).toBe(true);
    }
  });

  test("records input-to-ack latency for a live player intent", async ({ page }) => {
    await openWorld(page, liveUrl(`e2e-latency-${Date.now()}`));
    await expect(hudField(page, "connection")).toHaveText("Connected", { timeout: 20_000 });

    const ack = hudField(page, "input-ack");
    const center = await canvasCenter(page);
    for (let i = 0; i < 5; i++) {
      await page.mouse.click(center.x + (i - 2) * 32, center.y);
      await page.waitForTimeout(220);
    }

    await expect
      .poll(async () => (await ack.textContent()) ?? "", { timeout: 15_000 })
      .not.toBe("p50 0 / p95 0 ms");

    const text = (await ack.textContent()) ?? "";
    expect(text).toMatch(/p50 \d+ \/ p95 \d+ ms/);
    console.info(`[latency] ${text}`);
  });
});
