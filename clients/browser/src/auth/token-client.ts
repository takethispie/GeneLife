import type { ProblemResponse, TokenResponse } from "../contracts/rest";

export class AuthError extends Error {
  constructor(
    message: string,
    readonly status?: number
  ) {
    super(message);
    this.name = "AuthError";
  }
}

export interface PlayerToken {
  accessToken: string;
  tokenType: string;
  playerId: string;
  roles: string[];
  /** Unix epoch milliseconds. */
  expiresAt: number;
}

export type FetchLike = (input: string, init?: RequestInit) => Promise<Response>;

function isTokenResponse(value: unknown): value is TokenResponse {
  if (value === null || typeof value !== "object") return false;
  const record = value as Record<string, unknown>;
  return (
    typeof record.accessToken === "string" &&
    typeof record.tokenType === "string" &&
    typeof record.expiresAt === "string" &&
    typeof record.playerId === "string" &&
    Array.isArray(record.roles)
  );
}

export function parseTokenResponse(value: unknown): PlayerToken {
  if (!isTokenResponse(value)) {
    throw new AuthError("Token response is missing required fields");
  }
  const expiresAt = Date.parse(value.expiresAt);
  if (!Number.isFinite(expiresAt)) {
    throw new AuthError("Token response has an invalid expiresAt");
  }
  return {
    accessToken: value.accessToken,
    tokenType: value.tokenType,
    playerId: value.playerId,
    roles: value.roles.filter((role): role is string => typeof role === "string"),
    expiresAt
  };
}

/** Calls `POST /api/auth/token`. Never logs or persists the token here. */
export async function requestPlayerToken(
  apiBaseUrl: string,
  playerId: string,
  fetchImpl: FetchLike = fetch
): Promise<PlayerToken> {
  const trimmed = playerId.trim();
  if (trimmed === "") throw new AuthError("A player id is required");

  let response: Response;
  try {
    response = await fetchImpl(`${apiBaseUrl}/api/auth/token`, {
      method: "POST",
      headers: { "Content-Type": "application/json" },
      body: JSON.stringify({ playerId: trimmed })
    });
  } catch (error) {
    throw new AuthError(
      `Could not reach the auth endpoint: ${error instanceof Error ? error.message : "network error"}`
    );
  }

  if (!response.ok) {
    let problem: ProblemResponse | undefined;
    try {
      problem = (await response.json()) as ProblemResponse;
    } catch {
      problem = undefined;
    }
    throw new AuthError(problem?.message ?? problem?.error ?? "Authentication failed", response.status);
  }

  const body: unknown = await response.json();
  return parseTokenResponse(body);
}
