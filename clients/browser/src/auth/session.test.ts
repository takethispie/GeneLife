import { describe, expect, it, vi } from "vitest";
import {
  MemoryPersistence,
  SessionController,
  SessionStoragePersistence,
  SessionStore
} from "./session";
import { AuthError, parseTokenResponse, requestPlayerToken } from "./token-client";

const tokenBody = {
  accessToken: "token-abc",
  tokenType: "Bearer",
  expiresAt: "2030-01-01T00:00:00.000Z",
  playerId: "player-1",
  roles: ["Player"]
};

function jsonResponse(body: unknown, status = 200): Response {
  return new Response(JSON.stringify(body), {
    status,
    headers: { "Content-Type": "application/json" }
  });
}

describe("requestPlayerToken", () => {
  it("posts the player id and parses the expiry", async () => {
    const fetchImpl = vi.fn(async () => jsonResponse(tokenBody));
    const token = await requestPlayerToken("http://api.test", " player-1 ", fetchImpl);
    expect(fetchImpl).toHaveBeenCalledWith(
      "http://api.test/api/auth/token",
      expect.objectContaining({
        method: "POST",
        body: JSON.stringify({ playerId: "player-1" })
      })
    );
    expect(token.accessToken).toBe("token-abc");
    expect(token.expiresAt).toBe(Date.parse(tokenBody.expiresAt));
    expect(token.roles).toEqual(["Player"]);
  });

  it("surfaces the server problem message", async () => {
    const fetchImpl = vi.fn(async () =>
      jsonResponse({ message: "playerId is required" }, 400)
    );
    await expect(requestPlayerToken("http://api.test", "p", fetchImpl)).rejects.toThrowError(
      "playerId is required"
    );
  });

  it("rejects malformed token payloads", () => {
    expect(() => parseTokenResponse({ accessToken: "a" })).toThrowError(AuthError);
  });

  it("wraps network errors", async () => {
    const fetchImpl = vi.fn(async () => {
      throw new Error("offline");
    });
    await expect(requestPlayerToken("http://api.test", "p", fetchImpl)).rejects.toThrowError(
      /Could not reach the auth endpoint: offline/
    );
  });
});

describe("SessionController", () => {
  const now = Date.parse("2029-12-31T23:00:00.000Z");

  it("signs in, persists and restores a valid session", async () => {
    const persistence = new MemoryPersistence();
    const controller = new SessionController({
      apiBaseUrl: "http://api.test",
      persistence,
      now: () => now,
      fetchImpl: async () => jsonResponse(tokenBody)
    });

    const state = await controller.signIn("player-1");
    expect(state).toMatchObject({ status: "authenticated", playerId: "player-1" });
    expect(persistence.load()?.token).toBe("token-abc");

    const restored = new SessionController({
      apiBaseUrl: "http://api.test",
      persistence,
      now: () => now
    }).restore();
    expect(restored.status).toBe("authenticated");
  });

  it("rejects sign-in and clears stored state", async () => {
    const persistence = new MemoryPersistence();
    const controller = new SessionController({
      apiBaseUrl: "http://api.test",
      persistence,
      fetchImpl: async () => jsonResponse({ message: "nope" }, 403)
    });
    const state = await controller.signIn("player-1");
    expect(state.status).toBe("rejected");
    expect(persistence.load()).toBeNull();
  });

  it("refreshes expiring tokens", async () => {
    const calls: string[] = [];
    const controller = new SessionController({
      apiBaseUrl: "http://api.test",
      persistence: new MemoryPersistence(),
      now: () => Date.parse("2030-01-01T00:00:00.000Z"),
      refreshMarginMs: 60_000,
      fetchImpl: async () => {
        calls.push("token");
        return jsonResponse({ ...tokenBody, accessToken: `token-${calls.length}` });
      }
    });

    await controller.signIn("player-1");
    const token = await controller.ensureValidToken();
    expect(token).toBe("token-2");
    expect(calls).toHaveLength(2);
  });

  it("expires the session when refresh fails", async () => {
    let calls = 0;
    const controller = new SessionController({
      apiBaseUrl: "http://api.test",
      persistence: new MemoryPersistence(),
      now: () => Date.parse("2030-01-01T00:00:00.000Z"),
      refreshMarginMs: 60_000,
      fetchImpl: async () => {
        calls++;
        return calls === 1 ? jsonResponse(tokenBody) : jsonResponse({ message: "expired" }, 401);
      }
    });

    await controller.signIn("player-1");
    expect(await controller.ensureValidToken()).toBeNull();
    expect(controller.store.state.status).toBe("expired");
  });

  it("signs out and notifies subscribers", async () => {
    const store = new SessionStore();
    const seen: string[] = [];
    store.subscribe((state) => seen.push(state.status));
    const controller = new SessionController({
      apiBaseUrl: "http://api.test",
      store,
      persistence: new MemoryPersistence(),
      fetchImpl: async () => jsonResponse(tokenBody)
    });

    await controller.signIn("player-1");
    controller.signOut();
    expect(store.state.status).toBe("anonymous");
    expect(seen).toContain("authenticated");
    expect(seen[seen.length - 1]).toBe("anonymous");
  });

  it("restores expired sessions as expired", () => {
    const persistence = new MemoryPersistence();
    persistence.save({ playerId: "p", token: "t", expiresAt: 1 });
    const controller = new SessionController({
      apiBaseUrl: "http://api.test",
      persistence,
      now: () => 2
    });
    expect(controller.restore().status).toBe("expired");
    expect(persistence.load()).toBeNull();
  });

  it("persists through sessionStorage when available", () => {
    const persistence = new SessionStoragePersistence();
    persistence.save({ playerId: "p", token: "t", expiresAt: 123 });
    expect(persistence.load()).toEqual({ playerId: "p", token: "t", expiresAt: 123 });
    persistence.clear();
    expect(persistence.load()).toBeNull();
  });
});
