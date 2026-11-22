# How the labour market works

The economy does not invent jobs. Jobs exist because a company created an office with seats, and the company must pay for those seats whether or not they produce revenue.

## Participants

- **Company** — owns one or more offices, posts jobs, pays wages and taxes, can fail.
- **Office** — physical capacity. Each office has a number of seats and an operating cost. A company cannot employ more people than its offices can hold.
- **Job posting** — a request for a role at an office, with a salary, required skills and a number of open seats. Each seat is a separate `OpenPosition` entity, which is what `HiringSystem` already consumes.
- **Applicant** — a working-age NPC with a skill profile, a desired salary and a stage that permits work.
- **Employee** — the hired relationship: company, office, position and salary. `EmploymentIndex` is the read-side truth for who works where.

## The cycle

1. A company opens a posting because it has unmet capacity, expansion plans or a vacancy left by a separation.
2. Working-age NPCs without a job apply. The existing `HiringSystem` scores skill match and salary fit, then fills seats best-match first.
3. The hire creates an employment relationship and charges a recruitment cost to the company.
4. Every pay cycle, the company owes the sum of its employees' salaries. If it can pay, wages are paid and ledgered. If it cannot, the shortfall becomes arrears and the company enters distress.
5. Separations — firing, resignation, retirement, death — free a seat. The company decides whether to repost, downsize or close the office.

## What makes it feel realistic

- **Vacancies persist.** Hiring is not instant; applications are collected on a cadence and seats may stay open for several cycles.
- **Salary expectations move.** A worker's desired salary grows with skill and experience. A company that underbids loses candidates.
- **Capacity is physical.** A company with 50 seats and 50 employees must rent another office before it can grow.
- **Failure is visible.** Layoffs create job seekers; closures remove postings; unemployment rises.

## Admin levers

- create and close offices;
- open, edit and close postings;
- force a hire or a firing for scenario testing;
- freeze hiring for a company;
- set salary bands per position and seniority;
- inspect applications, seats, match scores and employment history.

## Out of scope

Unions, collective bargaining, commute time, remote work, gig work, multiple simultaneous jobs and informal labour. V1 models one full-time job per worker.
