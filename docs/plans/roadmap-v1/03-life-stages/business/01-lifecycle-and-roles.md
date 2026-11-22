# Lifecycle and roles

Every NPC moves through four periods. Age decides the role, and the role decides what the economy expects from them.

## The four periods

| Period | What society expects | What the economy does |
|---|---|---|
| Education | learn, depend on adults | consumes through a household, builds skills |
| Work | produce, earn, save | pays tax and pension, rents or buys, spends |
| Retirement | stop working, live on savings and pension | receives pension, spends less, may downsize |
| Elderly | decline, need care | draws down assets, uses health services, dies |

There is no fifth "child" stage. Early childhood is the first part of Education: the NPC exists, belongs to a household and becomes a student at school age.

## Transitions are events, not switches

A transition changes many things at once:

- components (job, schedule, pension, health modifiers);
- relationships (employer, school, household role);
- money (final wages, pension start, benefit eligibility);
- housing (income change can force a move).

If transitions are just an age check inside unrelated systems, the world drifts into impossible states: a 70-year-old employee, a student with a pension, a dead sim still counted as employed. The lifecycle must own the transition.

## Role consistency

The system enforces:

- a student cannot be an employee;
- a worker cannot receive a pension;
- a retiree cannot be a job seeker;
- an elder cannot be employed;
- a dead sim belongs to no household, job or school.

Violations are bugs, not gameplay. They are asserted in debug builds and reported by admin health checks.

## Where the lifecycle touches other features

- **Economy** — work supplies labour, payroll funds households, tax funds government, pension contributions fund retirement.
- **Housing** — income changes drive moves and evictions; dependents share a household; estate settlement releases units.
- **Health** — housing quality and age drive decline; mortality closes the loop.
- **Population** — inflow replaces deaths and fills labour demand; the age pyramid must stay plausible.

## Why this is the backbone of realism

A population is not interesting because it has many agents. It is interesting because agents change roles over time and those changes have consequences. The lifecycle is what turns a static crowd into a society with careers, families, retirement and inheritance.
