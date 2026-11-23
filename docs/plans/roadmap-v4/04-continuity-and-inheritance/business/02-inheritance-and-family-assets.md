# Inheritance and family assets

When a sim dies, the simulation already pays debts, funeral costs and taxes, then distributes what remains. Today the heirs are the people who happen to share the household. That is a proxy, not a family rule. V4 uses the relationship graph: partner, children, parents, siblings, then the state.

## Why relationships matter

- A partner may live in a different household while a merge is pending.
- A child may live with a guardian after a separation.
- A parent may have left the household years ago.
- A sibling may never have shared a home.

Household membership changes; parent and partner edges do not. Inheritance should follow the durable fact.

## Default order

1. Pay secured debts, rent/utility arrears and funeral costs.
2. Pay estate tax if configured.
3. Surviving partner receives the configured partner share.
4. Children share the residual equally.
5. If no partner or children, parents share.
6. If no parents, siblings share.
7. If no relatives, escheat to government.

The exact partner/child split is configured; the default in V4a is partner-first with children sharing the remainder.

## Minors

A child cannot receive a large sum directly. A minor's share is held in trust:

- the guardian manages it;
- it is recorded separately from the guardian's own money;
- it is released to the child at adulthood;
- the admin can see the balance and the guardian.

This is both a fairness rule and a conservation rule: the money must not disappear into a guardian's wallet.

## Housing

- An owned home transfers to the surviving partner first.
- If there is no partner, the guardian of a minor child holds it until the child is an adult.
- If there is no partner, child or guardian, the home is sold or escheated through the existing path.
- Renters do not inherit the unit; the household either continues or is released.

## Conservation

- Inheritance is a transfer, never a mint.
- A minor trust is included in total money supply.
- Funeral, tax and debt payments go to an existing destination.
- The 10-year balance scenario must show the same conservation gap of zero.

## What the operator should see

- the heir order and each share;
- a minor trust balance and its guardian;
- an owned home transfer;
- an escheat with the reason;
- any estate that could not be settled.