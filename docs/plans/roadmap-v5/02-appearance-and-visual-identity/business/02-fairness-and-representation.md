# Fairness and representation

Appearance is the most visible part of the trait system, and visible traits carry social meaning. V5 treats appearance as a representation of simulation state, not as a judgment about a person.

## Rules

- **No beauty score.** There is no attractiveness value derived from appearance in V5. Attraction remains the V4 explainable score, and appearance does not add a hidden ranking.
- **No public trait view.** Players see a face, not a trait sheet. Traits remain admin-only.
- **No player ranking.** The game does not sort, score or compare players by appearance.
- **No real-world identity claims.** Skin tone, face shape and hair are simulation categories, not statements about real people or groups.
- **No caricature.** The asset set must avoid exaggerated features that map a trait to a stereotype.
- **No pay-to-look.** Appearance is derived from traits; there is no cosmetic purchase that changes genetic resemblance.

## Representation in the asset set

- The palette covers a plausible range of skin tones and hair colours.
- Face shapes and body frames are varied without ranking one as better.
- Age bands represent children, adults and elders respectfully.
- Default clothing is role- and age-appropriate, not gendered in a way that limits work or family roles.
- The client provides a high-contrast and reduced-motion path; appearance must not be the only way to identify a sim.

## What operators should be able to do

- Preview any sim's appearance and the traits that produced it.
- Compare a family without exposing a public ranking.
- Audit the appearance version and cache state.
- Disable appearance rendering if a client cannot load the atlas, falling back to a labelled token.

## What operators should not do

- Change a sim's appearance without changing its traits, except through an explicit admin repair with an audit event.
- Use appearance as a hidden matchmaking or hiring signal.
- Publish appearance data on player routes.

## Success signal

A player can recognize their family and other players, and no one can turn appearance into a score, a ranking or a paid advantage.
