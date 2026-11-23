/**
 * REST types for the browser client.
 *
 * The canonical REST contract is generated from the running API's OpenAPI
 * document with `npm run contracts:rest`, which writes `rest.generated.ts`.
 * These hand-maintained types describe the small surface the game client uses
 * (auth and a health probe) so the client type-checks without a running server.
 * Regenerate and update this file when the API changes.
 */

import type { components } from "./rest.generated";

/**
 * Generated from the running API's OpenAPI document. Wolverine endpoints do not
 * emit response schemas, so the token response remains hand-maintained below.
 */
export type TokenRequest = components["schemas"]["TokenRequest"];

export interface TokenResponse {
  accessToken: string;
  tokenType: string;
  expiresAt: string;
  playerId: string;
  roles: string[];
}

export interface ProblemResponse {
  error?: string;
  message?: string;
}
