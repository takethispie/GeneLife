import { expect, test } from "@playwright/test";
import { canvasCenter, fixtureUrl, hudField, liveUrl, openWorld } from "./helpers";

test.describe("mobile touch controls", () => {
  test.use({ viewport: { width: 412, height: 915 }, hasTouch: true, isMobile: true });

  test("tap selects, touch settings apply and the HUD fits the viewport", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-mobile"));

    await expect(hudField(page, "connection")).toHaveText("Connected");
    await page.getByRole("button", { name: "Settings" }).click();
    await expect(page.locator(".hud-settings")).toBeVisible();
    await page.getByLabel("Touch controls").check();
    await expect(page.locator("html")).toHaveClass(/touch-mode/);
    await page.getByLabel("Touch handedness").selectOption("left");
    await expect(page.locator("html")).toHaveClass(/left-handed/);

    const settingsBox = await page.locator(".hud-settings").boundingBox();
    expect(settingsBox?.width ?? 0).toBeLessThanOrEqual(420);

    const center = await canvasCenter(page);
    await page.touchscreen.tap(center.x - 160, center.y - 160);
    await expect(page.locator(".selection-name")).toHaveText("Willow House");
  });

  test("pinch zooms and a drag pans without submitting a move", async ({ page }) => {
    await openWorld(page, fixtureUrl("e2e-mobile-gestures"));
    await page.keyboard.press("F3");
    const debug = page.locator(".debug-overlay");
    await expect(debug).toContainText("camera");
    const before = (await debug.textContent()) ?? "";

    const canvas = page.locator("#app canvas");
    const box = await canvas.boundingBox();
    if (box === null) throw new Error("canvas missing");
    const x = box.x + box.width / 2;
    const y = box.y + box.height / 2;

    await page.evaluate(
      ({ x, y }) => {
        const canvas = document.querySelector<HTMLCanvasElement>("#app canvas");
        if (canvas === null) throw new Error("no canvas");
        const fire = (type: string, id: number, px: number, py: number) => {
          canvas.dispatchEvent(
            new PointerEvent(type, {
              bubbles: true,
              pointerId: id,
              pointerType: "touch",
              clientX: px,
              clientY: py
            })
          );
        };
        fire("pointerdown", 1, x - 40, y);
        fire("pointerdown", 2, x + 40, y);
        fire("pointermove", 2, x + 120, y);
        fire("pointerup", 1, x - 40, y);
        fire("pointerup", 2, x + 120, y);
      },
      { x, y }
    );

    await expect
      .poll(async () => (await debug.textContent()) ?? "", { timeout: 5_000 })
      .not.toBe(before);
    await expect(page.locator(".notification-error")).toHaveCount(0);
  });
});

test.describe("mobile live touch latency", () => {
  test.skip(process.env.E2E_LIVE !== "1", "Set E2E_LIVE=1 with the API and Vite running");

  test("touch tap submits the same intent and records input-to-ack", async ({ page }) => {
    await openWorld(page, liveUrl(`e2e-mobile-live-${Date.now()}`));
    await expect(hudField(page, "connection")).toHaveText("Connected", { timeout: 20_000 });

    const center = await canvasCenter(page);
    await page.touchscreen.tap(center.x, center.y);
    await expect
      .poll(async () => (await hudField(page, "input-ack").textContent()) ?? "", { timeout: 15_000 })
      .not.toBe("p50 0 / p95 0 ms");
    console.info(`[touch-latency] ${await hudField(page, "input-ack").textContent()}`);
  });
});
