import { expect, test, type Page } from "@playwright/test";
import { clickTileOffset, fixtureUrl, openWorld } from "./helpers";

/**
 * PO requirement: the chat window is a persistent bottom-left MMO log, not a
 * dock menu item. These specs run in fixture mode (Vite only) and assert the
 * real geometry, which the jsdom unit tests cannot measure.
 */

const CHAT = ".social-chat";

async function bottomLeft(page: Page): Promise<{ x: number; bottomGap: number; height: number; width: number }> {
  const box = await page.locator(CHAT).boundingBox();
  if (box === null) throw new Error("chat window has no bounding box");
  const viewport = page.viewportSize();
  if (viewport === null) throw new Error("no viewport");
  return {
    x: box.x,
    bottomGap: viewport.height - (box.y + box.height),
    height: box.height,
    width: box.width
  };
}

test.describe("chat window", () => {
  test("mounts visible bottom-left without opening the dock", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-chat"));

    const chat = page.locator(CHAT);
    await expect(chat).toBeVisible();
    // The dock panel is closed: chat is not hidden behind a menu.
    await expect(page.locator(".hud-dock-panel")).toBeHidden();

    const viewport = page.viewportSize()!;
    const box = await bottomLeft(page);
    // Anchored near the left edge, not centred or right-aligned.
    expect(box.x).toBeLessThan(viewport.width / 4);
    // Bottom edge hugs the bottom of the screen.
    expect(box.bottomGap).toBeLessThan(40);
    // Chat-log sized: a few hundred px wide and not full height.
    expect(box.width).toBeGreaterThanOrEqual(280);
    expect(box.width).toBeLessThanOrEqual(380);
    expect(box.height).toBeLessThan(viewport.height * 0.7);
    expect(box.height).toBeGreaterThan(120);
  });

  test("stays bottom-left across resize and aspect ratios", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-chat"));

    for (const size of [
      { width: 1280, height: 720 },
      { width: 1440, height: 560 },
      { width: 900, height: 1000 }
    ]) {
      await page.setViewportSize(size);
      await expect(page.locator(CHAT)).toBeVisible();
      const box = await bottomLeft(page);
      expect(box.x).toBeLessThan(size.width / 4);
      expect(box.bottomGap).toBeLessThan(40);
    }
  });

  test("does not swallow world clicks outside the window", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-chat"));
    await expect(page.locator(CHAT)).toBeVisible();

    // A tile near the canvas centre is outside the bottom-left window.
    await clickTileOffset(page, -5, -5);
    await expect(page.locator(".selection-name")).toHaveText("Willow House");
  });

  test("does not swallow wheel zoom over the canvas", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-chat"));
    await page.keyboard.press("F3");
    const debug = page.locator(".debug-overlay");
    await expect(debug).toBeVisible();

    const before = await debug.textContent();
    const box = await page.locator("#app canvas").boundingBox();
    if (box === null) throw new Error("canvas missing");
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.wheel(0, -240);

    await expect.poll(() => debug.textContent(), { timeout: 5_000 }).not.toBe(before);
  });

  test("the dock chat icon toggles the window and other panels keep working", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-chat"));
    const chat = page.locator(CHAT);
    const toggle = page.locator('[data-dock-id="chat"]');
    await expect(toggle).toHaveAttribute("aria-pressed", "true");

    await toggle.click();
    await expect(chat).toBeHidden();
    await expect(toggle).toHaveAttribute("aria-pressed", "false");

    await toggle.click();
    await expect(chat).toBeVisible();

    // The other dock panels are unaffected.
    await page.getByRole("button", { name: "Settings" }).click();
    await expect(page.locator(".hud-settings")).toBeVisible();
  });

  test("captures a review screenshot of the bottom-left window", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-chat"));
    await expect(page.locator(CHAT)).toBeVisible();
    await page.screenshot({ path: "test-results-chat/chat-window-bottom-left.png" });
  });
});
