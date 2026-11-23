import { describe, expect, it } from "vitest";
import { ConfigError, parseConfig } from "./env";

describe("parseConfig", () => {
  it("derives the hub URL from the API base URL", () => {
    const config = parseConfig({
      VITE_API_BASE_URL: "http://localhost:5199/",
      VITE_LOG_LEVEL: "debug",
      VITE_DEV_PLAYER_ID: "tester",
      VITE_VIEWPORT_RADIUS: "48"
    });

    expect(config.apiBaseUrl).toBe("http://localhost:5199");
    expect(config.hubUrl).toBe("http://localhost:5199/hubs/world");
    expect(config.logLevel).toBe("debug");
    expect(config.devPlayerId).toBe("tester");
    expect(config.defaultViewportRadius).toBe(48);
    expect(config.useFixtures).toBe(false);
  });

  it("accepts an explicit hub URL", () => {
    const config = parseConfig({
      VITE_API_BASE_URL: "https://api.example.test",
      VITE_HUB_URL: "wss://api.example.test/hubs/world"
    });
    expect(config.hubUrl).toBe("wss://api.example.test/hubs/world");
  });

  it("requires an API base URL unless fixtures are enabled", () => {
    expect(() => parseConfig({})).toThrowError(ConfigError);
    expect(parseConfig({ VITE_USE_FIXTURES: "1" }).useFixtures).toBe(true);
  });

  it("rejects invalid URLs and log levels", () => {
    expect(() => parseConfig({ VITE_API_BASE_URL: "not a url" })).toThrowError(/not a valid URL/);
    expect(() =>
      parseConfig({ VITE_API_BASE_URL: "http://localhost:5199", VITE_LOG_LEVEL: "verbose" })
    ).toThrowError(/VITE_LOG_LEVEL/);
  });

  it("rejects invalid viewport radii", () => {
    expect(() =>
      parseConfig({ VITE_API_BASE_URL: "http://localhost:5199", VITE_VIEWPORT_RADIUS: "2" })
    ).toThrowError(/VITE_VIEWPORT_RADIUS/);
  });
});
