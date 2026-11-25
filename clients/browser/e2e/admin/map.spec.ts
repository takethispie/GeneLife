import { expect, test } from "@playwright/test";
import { mockMapApi } from "./map-api";

test.describe("admin map creation", () => {
  test("confirmation dialog gates replacing an existing map", async ({ page }) => {
    const api = await mockMapApi(page, { hasMap: true });

    await page.goto("/map");
    await expect(page.getByRole("heading", { name: "Map", level: 1 })).toBeVisible();

    await page.getByRole("button", { name: "Create map" }).click();

    const dialog = page.getByRole("dialog");
    await expect(dialog).toBeVisible();
    await expect(dialog.getByText("The current map, all buildings, all NPCs and all player characters")).toBeVisible();

    await dialog.getByRole("button", { name: "Cancel" }).click();
    await expect(dialog).toBeHidden();
    expect(api.calls.some((call) => call.method === "POST" && call.pathname === "/api/map/create")).toBe(false);

    await page.getByRole("button", { name: "Create map" }).click();
    await page.getByRole("button", { name: "Replace map" }).click();

    await expect(page.locator(".alert-success")).toContainText("Map created.");
    await expect(page.locator(".alert-success")).toContainText("192×192");
    await expect(page.getByText("world v5")).toBeVisible();
    expect(api.created).toBe(true);

    const createCall = api.calls.find(
      (call) => call.method === "POST" && call.pathname === "/api/map/create"
    );
    expect(createCall?.body).toMatchObject({ confirm: true, seed: 11, width: 256, height: 256 });
  });

  test("creating the first map skips the dialog", async ({ page }) => {
    const api = await mockMapApi(page, { hasMap: false });

    await page.goto("/map");
    await expect(page.getByRole("heading", { name: "Map", level: 1 })).toBeVisible();

    await page.getByRole("button", { name: "Create map" }).click();

    await expect(page.getByRole("dialog")).toBeHidden();
    await expect(page.locator(".alert-success")).toContainText("Map created.");

    const createCall = api.calls.find(
      (call) => call.method === "POST" && call.pathname === "/api/map/create"
    );
    expect(createCall?.body).toMatchObject({ confirm: false });
  });
});
