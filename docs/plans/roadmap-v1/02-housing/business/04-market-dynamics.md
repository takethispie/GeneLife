# Housing market dynamics

A fixed housing stock turns every population increase into a shortage. V1 adds a slow supply response so the world can absorb growth and immigration.

## Supply

Housing is built by construction companies when:

- vacancy falls below a threshold (default 3%);
- expected rent covers construction cost plus a margin;
- the company has capital or credit;
- land/zoning allows it (V1: a simple district capacity cap).

Construction takes time (months), consumes capital and produces new units. Units enter the market at a quality tier determined by expected rent.

## Demand

Demand comes from:

- new households formed by population generation or immigration;
- household splits (future);
- moves from overcrowded or unaffordable units;
- replacement of demolished or uninhabitable units.

## Rent setting

Landlords set rent from a base by quality tier multiplied by a market index:

```
rent = baseRent[tier] × marketIndex × neighbourhoodMultiplier × conditionMultiplier
marketIndex moves toward targetVacancy
```

- High vacancy → index falls.
- Low vacancy → index rises, capped to avoid runaway rents.
- Rent changes are applied at most once per year per unit, so tenants are not whipsawed monthly.

## Vacancy and frictions

- A vacant unit costs the landlord maintenance but earns nothing, which pushes rents down.
- A household cannot instantly take a unit: there is a search delay and a deposit.
- Shelter absorbs the bottom of the market; it is not a substitute for housing.

## Failure modes

- **No supply response** → permanent shortage and homelessness.
- **Instant supply** → rents collapse and housing becomes meaningless.
- **Rent controls without supply** → queues and decay; out of scope for V1.
- **Speculation** → out of scope; owners do not buy to hold vacant.

## Admin controls

- create or remove properties;
- set rent manually;
- trigger construction;
- adjust market index bounds;
- inspect vacancy, rent index, construction pipeline and landlord returns.

The market is deliberately slow. It should respond over simulated years, not within a single pay cycle.
