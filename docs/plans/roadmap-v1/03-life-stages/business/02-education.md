# Education

Education is the pipeline that turns children into workers with differentiated skills. Without it, every new worker is identical and the labour market has no matching problem.

## What education does

- enrolls children in a school at age 6;
- teaches a curriculum weighted by school quality and household background;
- produces a skill profile at graduation;
- gates the kinds of jobs a graduate can realistically get;
- costs money, funded by the public budget or tuition.

## Schools

A school has:

- capacity and a catchment district;
- quality (0–1);
- curriculum emphasis (technical, service, creative, general);
- operating cost paid by the government or a private owner.

A child attends the nearest school with capacity. If none exists, they attend a lower-quality fallback or receive no formal education, which shows up as weaker skills and lower lifetime earnings.

## Skills

V1 uses six skill families already present in `Skill`: programming, art, cooking, medical, engineering, sales. The bitmask says *which* skills a person has; a proficiency profile says *how good* they are.

Education raises proficiency slowly and unevenly:

- core subjects raise general competence;
- school emphasis raises specific families;
- household wealth modestly improves outcomes;
- each year adds a bounded amount, so a full education produces a plausible spread, not a maxed character.

## Graduation

At 18, the student graduates with:

- a proficiency profile;
- a desired salary derived from skills and household expectations;
- a `JobSeeker` entry into the labour market.

There is no exam failure in V1. Variation comes from school quality, attendance and household background, not from a pass/fail gate.

## What the player sees

- children go to school during the day;
- teenagers develop strengths;
- graduates compete for jobs that match those strengths;
- low-skill workers earn less over a lifetime and are more exposed to unemployment.

## What the admin sees

- enrollment and capacity by school;
- skill distribution of graduates;
- schools with low quality or overcrowding;
- graduate outcomes: employment rate and starting salary by school;
- the ability to create a school, adjust quality, enroll a sim or force graduation.

## Out of scope

Universities, vocational tracks, tuition loans, dropouts, adult education and re-skilling. V1 has one school tier and one graduation point.
