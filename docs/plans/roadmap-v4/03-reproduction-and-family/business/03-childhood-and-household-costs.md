# Childhood and household costs

A child is not a free extra resident. Food, school, health care and space all have costs, and the household or the state has to pay them. V4 keeps those costs inside the existing money loop instead of inventing a parallel child economy.

## The dependency period

- A newborn is age 0 and dependent.
- School starts at `SchoolStartAge` (currently 6).
- Work starts at `WorkStartAge` (currently 18).
- The guardian household provides the residence and the budget.
- Parent edges do not disappear when a child moves or a guardian changes.

## What costs exist

| Cost | Source | Who pays |
|---|---|---|
| Food and household consumption | household consumption budget | household |
| Rent and utilities | existing housing system | household |
| School | `School.MonthlyCost` | government, company owner or household, per existing policy |
| Health care | existing health system | household or government, per existing policy |
| Child benefit (optional) | government transfer | guardian household |

V4a should reuse existing flows before adding a new benefit. If a child benefit is added, it is a ledgered government transfer with a visible budget and no minting.

## What happens when the household cannot pay

- The existing arrears, health and needs systems apply.
- A child does not get a special exemption from hunger or illness.
- The state may shelter the household through the existing shelter path.
- A child without a capable guardian becomes a state-care case in feature 04.

## What the child does

- Age 0–5: dependent, at home, no school enrollment.
- Age 6–17: education, school enrollment, skill growth.
- Age 18: work stage, job search, adult household options.
- A child never holds a job, pension or independent tenancy.

## What the operator should see

- household size and child count;
- school enrollment by age;
- child costs and arrears;
- children in overcrowded or sheltered housing;
- children without a guardian;
- the transition from education to work.

## Why this matters for balance

Births change household size, consumption and school demand. If child costs are free, the population grows without economic consequence. If they are punitive, births collapse. The target is visible, ledgered cost with a survivable but real budget pressure.