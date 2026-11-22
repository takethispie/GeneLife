# Tenure and mobility

Not every NPC relates to housing the same way. Tenure determines exposure to the market and what happens when money runs out.

## Tenure types

| Tenure | Who | Monthly cost | Risk |
|---|---|---|---|
| Dependent | children, students living with family | none directly | household moves or dissolves |
| Renter | most workers | rent + utilities | eviction on persistent arrears |
| Owner | wealthy workers and retirees | maintenance + mortgage | foreclosure only on mortgage default |
| Sheltered | homeless households | none or nominal | capacity, poor conditions |
| Institutional | elderly care (future) | fee | out of scope for V1 |

## Mobility

Households move for predictable reasons:

- a better job in another district (future, needs commute);
- rent becomes unaffordable;
- household size changes;
- quality dissatisfaction;
- eviction.

V1 models **economic moves** only: a household moves when rent burden exceeds a threshold and a cheaper suitable unit is vacant, or after eviction when a shelter or cheaper unit is available. Moves have a cost (deposit plus moving fee), so they are not free.

## Why mobility matters

Without moving:

- a household stuck in an expensive unit slowly drains and eventually becomes homeless for no visible reason;
- vacancy never clears;
- the housing market has no price signal.

With moving:

- rent burden pushes households down-market;
- vacant units get filled;
- eviction has somewhere to go;
- the admin can watch a housing ladder instead of a binary housed/homeless state.

## Ownership

Ownership is a store of wealth and a retirement asset. V1 allows purchase only for households above a wealth threshold and only with cash; mortgages are a later step. Owners pay maintenance and property tax but no rent, and their home counts in estate settlement.

## Failure modes

- **Eviction without alternatives.** Always create shelter capacity or a cheaper vacancy path, or the population collapses.
- **Free moves.** Moving must cost money, otherwise rent burden has no friction.
- **Hidden subletting.** One unit, one household, one tenancy record.
