import { expect, type Page } from "@playwright/test";

export function fixtureUrl(playerId: string): string {
  return `/?fixtures=1&playerId=${encodeURIComponent(playerId)}`;
}

export function liveUrl(playerId: string): string {
  return `/?playerId=${encodeURIComponent(playerId)}`;
}

export function hudField(page: Page, field: string) {
  return page.locator(`#hud [data-field="${field}"]`);
}

export async function openWorld(page: Page, url: string): Promise<void> {
  await page.goto(url);
  await expect(page.locator("#loading")).toBeHidden({ timeout: 20_000 });
  await expect(page.locator("#app canvas")).toBeVisible();
}

export async function canvasCenter(page: Page): Promise<{ x: number; y: number }> {
  const box = await page.locator("#app canvas").boundingBox();
  if (box === null) throw new Error("canvas has no bounding box");
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

export async function clickTileOffset(
  page: Page,
  tileX: number,
  tileY: number,
  zoom = 32
): Promise<void> {
  const center = await canvasCenter(page);
  await page.mouse.click(center.x + tileX * zoom, center.y + tileY * zoom);
}
