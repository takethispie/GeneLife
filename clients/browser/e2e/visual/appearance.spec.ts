import { expect, test, type Locator, type Page } from "@playwright/test";

/**
 * V5 feature 02 appearance acceptance — Playwright visual smoke.
 *
 * Runs against the `appearance` fixture set, which walks every value of every
 * appearance dimension at least once (see scripts/generate-appearance-fixtures.mjs).
 * Before that set existed no fixture carried an `appearance` field at all, so
 * `composeAppearance` always fell back to its defaults and the sprite layer had
 * no coverage.
 *
 * The scene is frozen with the "Reduce motion" setting so baselines are stable,
 * and the HUD's live counters are masked because they change every frame.
 */

const FIXTURE_SET = "appearance";
const EXPECTED_GENOSES = 45;

/** HUD rows that update every frame and would make every baseline flaky. */
const DYNAMIC_HUD_FIELDS = ["fps", "frame", "buffer", "interp", "input-ack", "rate", "viewport-r"];

function appearanceUrl(playerId: string): string {
  return `/?fixtures=1&fixtureSet=${FIXTURE_SET}&playerId=${encodeURIComponent(playerId)}`;
}

function dynamicHudMask(page: Page): Locator[] {
  return DYNAMIC_HUD_FIELDS.map((field) => page.locator(`#hud [data-field="${field}"]`));
}

async function openAppearanceWorld(page: Page): Promise<string[]> {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(`console: ${message.text()}`);
  });

  await page.goto(appearanceUrl("visual-appearance"));
  await expect(page.locator("#loading")).toBeHidden({ timeout: 20_000 });
  await expect(page.locator("#app canvas")).toBeVisible();

  // Freeze animation and interpolation so the baselines are reproducible.
  // The dock button is addressed by id: once the panel is open, the accessible
  // name "Settings" also matches its "Reset settings" button.
  const settingsToggle = page.locator('[data-dock-id="settings"]');
  await settingsToggle.click();
  await page.getByLabel("Reduce motion").check();
  await expect(page.locator("html")).toHaveClass(/reduce-motion/);
  await settingsToggle.click();

  // The appearance set must actually be loaded — otherwise this suite would
  // happily baseline the default five-genos fixture and prove nothing. The HUD
  // renders this field as "<visible>/<total>".
  await expect(page.locator('#hud [data-field="genoses"]')).toHaveText(
    new RegExp(`/${EXPECTED_GENOSES}$`)
  );

  return errors;
}

/**
 * Hides the HUD so a canvas screenshot is a pure render baseline. Playwright's
 * element screenshot captures the element's page region including anything
 * drawn over it, so without this the "canvas" baseline would be byte-identical
 * to the full-page one (the HUD rail overlays the canvas).
 */
async function hideHud(page: Page): Promise<void> {
  await page.addStyleTag({ content: "#hud { visibility: hidden !important; }" });
}

test.describe("appearance visual smoke", () => {
  test("the appearance grid renders with no console errors", async ({ page }) => {
    const errors = await openAppearanceWorld(page);
    await expect(page.locator('#hud [data-field="connection"]')).toHaveText("Connected");
    expect(errors).toEqual([]);
  });

  test("the world and HUD match the baseline", async ({ page }) => {
    await openAppearanceWorld(page);
    await expect(page).toHaveScreenshot("appearance-world.png", {
      mask: dynamicHudMask(page)
    });
  });

  test("the genos sprite layer matches the baseline", async ({ page }) => {
    await openAppearanceWorld(page);
    await hideHud(page);
    await expect(page.locator("#app canvas")).toHaveScreenshot("appearance-sprites.png");
  });

  test("zoomed appearance detail matches the baseline", async ({ page }) => {
    await openAppearanceWorld(page);

    const canvas = page.locator("#app canvas");
    const box = await canvas.boundingBox();
    if (box === null) throw new Error("canvas has no bounding box");

    // Zoom in so individual part/palette combinations are legible.
    await page.mouse.move(box.x + box.width / 2, box.y + box.height / 2);
    await page.mouse.wheel(0, -480);
    await page.waitForTimeout(500);

    await hideHud(page);
    await expect(canvas).toHaveScreenshot("appearance-sprites-zoomed.png");
  });
});
