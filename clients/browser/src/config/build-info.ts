/** Injected by Vite at build time (`vite.config.ts`). */
declare const __BUILD_STAMP__: string;

export function buildStamp(): string {
  return typeof __BUILD_STAMP__ === "string" && __BUILD_STAMP__ !== ""
    ? __BUILD_STAMP__
    : "dev";
}

/** Client protocol version expected by the hub `Welcome` message. */
export const CLIENT_PROTOCOL_VERSION = 1;

export interface BuildInfo {
  stamp: string;
  protocolVersion: number;
}

export function buildInfo(): BuildInfo {
  return { stamp: buildStamp(), protocolVersion: CLIENT_PROTOCOL_VERSION };
}
