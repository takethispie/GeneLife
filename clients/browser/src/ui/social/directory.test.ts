import { afterEach, describe, expect, it, vi } from "vitest";
import { PlayerDirectoryPanel } from "./directory";

describe("player directory", () => {
  afterEach(() => {
    document.body.replaceChildren();
    vi.restoreAllMocks();
  });

  it("renders only bounded public presence fields", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const fetchImpl = vi.fn(() => Promise.resolve(new Response(JSON.stringify({
      scope: "Nearby",
      radius: 12,
      items: [{ playerId: "bob", displayName: "Bob", genosNetworkId: 2, tileX: 3, tileY: 4, buildingNetworkId: null, state: "Online" }]
    }), { status: 200 })));
    const panel = new PlayerDirectoryPanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl,
      intervalMs: 60_000
    });

    await panel.refresh();
    const text = root.textContent ?? "";
    expect(text).toContain("Bob");
    expect(text).toContain("Online");
    expect(text).not.toContain("wallet");
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://localhost/api/social/presence?scope=nearby&radius=12",
      expect.objectContaining({ headers: { Authorization: "Bearer token" } })
    );
    panel.dispose();
  });

  it("shows the generic resident when the display name is hidden", async () => {
    const root = document.createElement("div");
    document.body.appendChild(root);
    const panel = new PlayerDirectoryPanel({
      root,
      apiBaseUrl: "http://localhost",
      token: () => "token",
      fetchImpl: vi.fn(() => Promise.resolve(new Response(JSON.stringify({
        scope: "Nearby",
        radius: 12,
        items: [{ playerId: "bob", displayName: "Resident", genosNetworkId: 2, tileX: 3, tileY: 4, buildingNetworkId: null, state: "Online" }]
      }), { status: 200 }))),
      intervalMs: 60_000
    });
    await panel.refresh();
    expect(root.textContent).toContain("Resident");
    panel.dispose();
  });
});
