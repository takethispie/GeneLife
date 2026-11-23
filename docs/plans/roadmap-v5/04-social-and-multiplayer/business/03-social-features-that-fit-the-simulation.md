# Social features that fit the simulation

A generic social layer would compete with the simulation. V5 uses the world the player is already in: streets, homes, offices, schools, shops and civic spaces.

## What fits

- **Nearby presence.** Players see each other because they are in the same place, not because they share a global list.
- **Local chat.** Conversation is scoped to a venue or a short radius.
- **Emotes.** A small set of actions that read in the world: wave, nod, sit, cheer.
- **Friendships.** A persistent social edge that survives reconnect and supports direct messages.
- **Player partnerships.** A consensual relationship that uses the V4 partner edge and the same household rules.
- **Shared venues.** A café, park or civic building where players can meet and see who is there.
- **Activities.** A small set of proximity actions at a venue: sit, talk, share a meal, play a game.

## What does not fit

- A global lobby disconnected from the map.
- Matchmaking based on traits or appearance.
- A dating feed or public ranking.
- A follower graph or influencer mechanics.
- Voice chat in V5a.
- Player-owned social spaces before the venue model is stable.

## How social connects to the simulation

- Presence uses the player's sim position and current building.
- Local chat uses the same tile/viewport rules as the world.
- Friendships and partnerships are persisted edges, not UI-only state.
- A player partnership follows the V4 eligibility, consent and household rules.
- Venue activities can emit simulation events, but they do not mint money or bypass needs.

## What the player should notice

- Social life happens where their sim is.
- The people they meet are the people they see.
- A friendship or partnership is a fact the world remembers.
- The social layer does not replace the economy, family or housing systems.
