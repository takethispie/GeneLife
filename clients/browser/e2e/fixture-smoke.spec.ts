import { expect, test } from "@playwright/test";
import { canvasCenter, clickTileOffset, fixtureUrl, hudField, openWorld } from "./helpers";

test.describe("fixture-mode browser smoke", () => {
  test("renders the world, HUD and building selection without console errors", async ({ page }) => {
    const errors: string[] = [];
    page.on("pageerror", (error) => errors.push(error.message));
    page.on("console", (message) => {
      if (message.type() === "error") errors.push(message.text());
    });

    await openWorld(page, fixtureUrl("e2e-fixture"));

    await expect(hudField(page, "connection")).toHaveText("Connected");
    await expect(hudField(page, "player")).toHaveText("e2e-fixture");
    await expect(hudField(page, "genos")).toHaveText("1");
    await expect(hudField(page, "map")).toHaveText("world");
    await expect(hudField(page, "version")).toHaveText("3");
    await expect(hudField(page, "tick")).toHaveText("100");

    await clickTileOffset(page, -5, -5);
    await expect(page.locator(".selection-name")).toHaveText("Willow House");
    await expect(page.locator(".selection-owner")).toHaveText("Owner #42");

    await page.locator("#hud button", { hasText: "Clear selection" }).click();
    await expect(page.locator(".selection")).toBeHidden();

    expect(errors).toEqual([]);
  });

  test("camera keys, wheel zoom, debug overlay and settings work", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-controls"));

    await page.keyboard.press("F3");
    const debug = page.locator(".debug-overlay");
    await expect(debug).toBeVisible();
    await expect(debug).toContainText("fps");

    const beforeZoom = await debug.textContent();
    const center = page.locator("#app canvas");
    const box = await center.boundingBox();
    if (box === null) throw new Error("canvas missing");
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.wheel(0, -240);
    await expect
      .poll(async () => debug.textContent(), { timeout: 5_000 })
      .not.toBe(beforeZoom);

    const beforePan = await debug.textContent();
    await page.keyboard.down("d");
    await page.waitForTimeout(400);
    await page.keyboard.up("d");
    await expect
      .poll(async () => debug.textContent(), { timeout: 5_000 })
      .not.toBe(beforePan);

    await page.getByRole("button", { name: "Settings" }).click();
    await expect(page.locator(".hud-settings")).toBeVisible();
    await page.getByLabel("Reduce motion").check();
    await expect(page.locator("html")).toHaveClass(/reduce-motion/);
    await page.getByLabel("Debug overlay").uncheck();
    await expect(debug).toBeHidden();
    await page.getByRole("button", { name: "Reset settings" }).click();
  });

  test("out-of-bounds clicks explain themselves without selecting anything", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-bounds"));
    const canvas = page.locator("#app canvas");
    const box = await canvas.boundingBox();
    if (box === null) throw new Error("canvas missing");
    await page.mouse.click(box.x + 40, box.y + box.height / 2);
    await expect(page.locator(".notification").last()).toContainText("That is outside the map");
  });

  test("right-click moves while left-click only selects", async ({ page }) => {
    // Fixture mode has no intent endpoint, so a move is rejected with a visible
    // error while a selection builds a panel and stays silent.
    await openWorld(page, fixtureUrl("e2e-right-click"));
    const center = await canvasCenter(page);
    await page.mouse.click(center.x, center.y, { button: "right" });
    await expect(page.locator(".notification-error").last()).toContainText("no intent endpoint");
  });

  test("left-click never issues a move", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-left-click"));
    const center = await canvasCenter(page);
    await page.mouse.click(center.x, center.y);
    await page.waitForTimeout(400);
    await expect(page.locator(".notification-error")).toHaveCount(0);
  });
});
