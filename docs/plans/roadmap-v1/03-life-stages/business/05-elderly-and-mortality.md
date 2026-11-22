# Elderly and mortality

The elderly stage is the end of the lifecycle. It should feel like decline, not a timer: health worsens, needs are harder to meet, and death arrives from a combination of age, health, poverty and housing.

## Decline

Elders (80+):

- recover more slowly from needs;
- lose health gradually;
- are more sensitive to bad housing and cold/poor conditions;
- spend less and rely on savings and pension;
- may need household support to remain housed.

Decline scales with age, so an 82-year-old is not the same as a 95-year-old. The system uses a continuous factor rather than a cliff.

## Health

Health is a 0–100 resource that no other system fully restores. It drifts based on:

- age;
- housing quality and overcrowding;
- unmet needs;
- access to medical care (purchased by the household);
- poverty.

Low health reduces action efficiency and raises mortality risk. It is the bridge between housing, economy and death.

## Death

Death has two paths:

1. **Stochastic mortality** — a periodic risk based on age, health, wealth and housing. It rises steeply after 80 and is capped so death is not arbitrary.
2. **Needs depletion** — a sim whose critical need stays at zero while idle and unable to restore it dies. This can happen at any age and is the consequence of extreme poverty or neglect.

Death is processed through a queue, not inside a query:

- remove from employment, school, household and housing;
- release seats and units;
- queue estate settlement;
- emit a death event with cause and age.

## Estate settlement

When a sim dies:

1. pay outstanding debts from cash and savings;
2. transfer the remainder to household members, then relatives, then a means-tested pool;
3. if no heir exists, escheat to the government;
4. release or transfer housing;
5. emit a settlement report.

This keeps money circulating and makes inheritance a real economic force. It also prevents dead sims from holding apartments forever.

## Why this matters

- It closes the lifecycle.
- It creates inheritance and wealth redistribution.
- It makes housing quality and poverty lethal over long runs.
- It forces population inflow to replace losses.
- It gives the admin a mortality log to debug population collapse.

## Out of scope

Disease models, hospitals as separate institutions, funeral costs, wills and multi-generation family trees. V1 uses household and a simple heir order.
