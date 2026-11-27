import type { GenosStatus } from "../contracts/hub";

export interface GenosDetails {
  name: string;
  /** "Alive" or "Deceased" — the genos' dead state, always shown. */
  status: string;
  /** Age, life stage and health. */
  meta: string;
  /** Current action, with the time left when one is running. */
  action: string;
  /** The four needs, in the order the server sends them. */
  needs: string;
  /** Wallet and savings balances. */
  money: string;
  /** Housing tier and tenure, or "homeless". */
  housing: string;
  /** Household membership, or "No household". */
  household: string;
  /** Housing unit, or "No housing unit". */
  housingUnit: string;
  hint: string;
}

/**
 * Presentation text for the genos inspection panel. Every string here comes
 * from the client's `GenosStatus` snapshot — no server call is made — and is
 * always rendered with `textContent` (genos names are server-provided).
 */
export function describeGenos(genos: GenosStatus): GenosDetails {
  const action =
    genos.timeRemaining > 0
      ? `${genos.currentAction || "Idle"} · ${genos.timeRemaining}s left`
      : genos.currentAction || "Idle";

  const housing = genos.homeless
    ? "homeless"
    : `${genos.housingTier || "unknown tier"} · ${genos.tenure || "unknown tenure"}`;

  return {
    name: genos.name || "Genos",
    status: genos.isDead ? "Deceased" : "Alive",
    meta: `age ${genos.age} · ${genos.stage || "unknown stage"} · health ${genos.health}`,
    action,
    needs:
      `hunger ${genos.hunger} · energy ${genos.energy} · ` +
      `hygiene ${genos.hygiene} · bladder ${genos.bladder}`,
    money: `wallet ${genos.walletBalance} · savings ${genos.savingsBalance}`,
    housing,
    household:
      genos.householdNetworkId === undefined || genos.householdNetworkId === null
        ? "No household"
        : `Household #${genos.householdNetworkId}`,
    housingUnit:
      genos.housingUnitNetworkId === undefined || genos.housingUnitNetworkId === null
        ? "No housing unit"
        : `Housing unit #${genos.housingUnitNetworkId}`,
    hint: "Live from the latest snapshot. Left-click empty ground or press Escape to close."
  };
}
