import { expect, test, type Page } from "@playwright/test";

/**
 * Density regression guard for the poison case from the reporter's screenshot:
 * 2,000 named genoses (the `stress` fixture set, ~2 per tile) at the default
 * zoom of 32, where every genos used to get a full-alpha nameplate and a sprite
 * big enough to tile over the ground.
 *
 * The frame this baselines is the evidence the PO asked for: if a future change
 * makes the sprites cover the ground again, or lets the nameplates grow back
 * into solid rows of text, this baseline fails.
 *
 * The scene is frozen with "Reduce motion" so positions and interpolation are
 * reproducible, and the HUD is hidden for the canvas shot so the baseline is
 * pure render output.
 */

const FIXTURE_SET = "stress";
const EXPECTED_GENOSES = 2000;

function stressUrl(playerId: string): string {
  return `/?fixtures=1&fixtureSet=${FIXTURE_SET}&playerId=${encodeURIComponent(playerId)}`;
}

async function openStressWorld(page: Page): Promise<string[]> {
  const errors: string[] = [];
  page.on("pageerror", (error) => errors.push(`pageerror: ${error.message}`));
  page.on("console", (message) => {
    if (message.type() === "error") errors.push(`console: ${message.text()}`);
  });

  await page.goto(stressUrl("visual-stress"));
  await expect(page.locator("#loading")).toBeHidden({ timeout: 20_000 });
  await expect(page.locator("#app canvas")).toBeVisible();

  // Freeze animation/interpolation so the baselines are reproducible.
  const settingsToggle = page.locator('[data-dock-id="settings"]');
  await settingsToggle.click();
  await page.getByLabel("Reduce motion").check();
  await expect(page.locator("html")).toHaveClass(/reduce-motion/);
  await settingsToggle.click();

  // The stress set must actually be loaded, or this baseline would happily
  // prove nothing.
  await expect(page.locator('#hud [data-field="genoses"]')).toHaveText(
    new RegExp(`/${EXPECTED_GENOSES}$`)
  );

  return errors;
}

test.describe("stress density render", () => {
  test("the stress world renders with no console errors", async ({ page }) => {
    const errors = await openStressWorld(page);
    await expect(page.locator('#hud [data-field="connection"]')).toHaveText("Connected");
    expect(errors).toEqual([]);
  });

  test("the map stays readable under 2,000 genoses at zoom 32", async ({ page }) => {
    await openStressWorld(page);
    await page.addStyleTag({ content: "#hud { visibility: hidden !important; }" });
    await expect(page.locator("#app canvas")).toHaveScreenshot("stress-density.png");
  });
});
