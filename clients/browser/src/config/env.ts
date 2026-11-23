export type LogLevel = "debug" | "info" | "warn" | "error";

export interface RawClientEnv {
  VITE_API_BASE_URL?: string;
  VITE_HUB_URL?: string;
  VITE_LOG_LEVEL?: string;
  VITE_USE_FIXTURES?: string;
  VITE_DEV_PLAYER_ID?: string;
  VITE_VIEWPORT_RADIUS?: string;
}

export interface ClientConfig {
  apiBaseUrl: string;
  hubUrl: string;
  logLevel: LogLevel;
  useFixtures: boolean;
  devPlayerId: string;
  defaultViewportRadius: number;
}

export class ConfigError extends Error {
  constructor(message: string) {
    super(message);
    this.name = "ConfigError";
  }
}

function trimTrailingSlash(value: string): string {
  return value.replace(/\/+$/, "");
}

function parseBoolean(value: string | undefined, fallback: boolean): boolean {
  if (value === undefined || value === "") return fallback;
  if (value === "true" || value === "1") return true;
  if (value === "false" || value === "0") return false;
  throw new ConfigError(`expected a boolean, got "${value}"`);
}

function parseLogLevel(value: string | undefined): LogLevel {
  if (value === undefined || value === "") return "info";
  if (value === "debug" || value === "info" || value === "warn" || value === "error") return value;
  throw new ConfigError(`VITE_LOG_LEVEL must be debug, info, warn or error (got "${value}")`);
}

function parseUrl(value: string, name: string, allowWebSocket = false): string {
  try {
    const url = new URL(value);
    const allowed = allowWebSocket
      ? ["http:", "https:", "ws:", "wss:"]
      : ["http:", "https:"];
    if (!allowed.includes(url.protocol)) {
      throw new ConfigError(
        `${name} must use ${allowWebSocket ? "http, https, ws or wss" : "http or https"}`
      );
    }
    return trimTrailingSlash(url.toString());
  } catch (error) {
    if (error instanceof ConfigError) throw error;
    throw new ConfigError(`${name} is not a valid URL: "${value}"`);
  }
}

function parseRadius(value: string | undefined): number {
  if (value === undefined || value === "") return 32;
  const parsed = Number(value);
  if (!Number.isFinite(parsed) || parsed < 4) {
    throw new ConfigError(`VITE_VIEWPORT_RADIUS must be a number >= 4 (got "${value}")`);
  }
  return Math.floor(parsed);
}

/**
 * Validates runtime configuration at startup. Missing API/HUB settings fail
 * fast with a readable message instead of producing failing requests later.
 */
export function parseConfig(raw: RawClientEnv): ClientConfig {
  const useFixtures = parseBoolean(raw.VITE_USE_FIXTURES, false);
  const apiBaseUrlRaw = raw.VITE_API_BASE_URL?.trim() ?? "";

  if (!useFixtures && apiBaseUrlRaw === "") {
    throw new ConfigError("VITE_API_BASE_URL is required (or set VITE_USE_FIXTURES=true)");
  }

  const apiBaseUrl = apiBaseUrlRaw === "" ? "" : parseUrl(apiBaseUrlRaw, "VITE_API_BASE_URL");
  const hubUrlRaw = raw.VITE_HUB_URL?.trim() ?? "";
  const hubUrl =
    hubUrlRaw === ""
      ? apiBaseUrl === ""
        ? ""
        : `${apiBaseUrl}/hubs/world`
      : parseUrl(hubUrlRaw, "VITE_HUB_URL", true);

  if (!useFixtures && hubUrl === "") {
    throw new ConfigError("VITE_HUB_URL is required (or set VITE_USE_FIXTURES=true)");
  }

  return {
    apiBaseUrl,
    hubUrl,
    logLevel: parseLogLevel(raw.VITE_LOG_LEVEL),
    useFixtures,
    devPlayerId: raw.VITE_DEV_PLAYER_ID?.trim() || "dev-player",
    defaultViewportRadius: parseRadius(raw.VITE_VIEWPORT_RADIUS)
  };
}

export function loadRuntimeConfig(): ClientConfig {
  const env: RawClientEnv = { ...(import.meta.env as RawClientEnv) };
  if (typeof window !== "undefined") {
    try {
      if (new URL(window.location.href).searchParams.get("fixtures") === "1") {
        env.VITE_USE_FIXTURES = "true";
      }
    } catch {
      // Keep the build-time configuration.
    }
  }
  return parseConfig(env);
}
