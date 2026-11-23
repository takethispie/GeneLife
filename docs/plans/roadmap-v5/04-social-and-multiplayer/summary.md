# Social and Multiplayer — Summary

**Folder:** `04-social-and-multiplayer/`
**Status:** Planned (V5 feature 04)

**Depends on:** V3 hub/presence/reconnect, feature 01 player identity and family invitations, feature 02 appearance, schema 14
**Feeds:** live operations and scale, player retention, moderation operations

## Objective

Let players see and interact with each other in the shared world with explicit consent, clear privacy boundaries and moderation from the first release. Social play should grow out of the simulation — proximity, shared venues, friendships and consensual partnerships — not a separate lobby or social network.

This feature adds presence, text chat, emotes, friendships, blocks, reports, consensual player partnerships and shared-venue activities. It does not add matchmaking, a dating feed, voice chat or a global social feed.

## Baseline before V5

- The world supports up to 32 concurrent players with server-authoritative presence.
- `WorldHub` handles connect, viewport, snapshots, deltas, reconnect and player intents.
- `PlayerControlled` binds a player to one sim; the hub knows the connection.
- There is no player directory, chat, emote, social graph, block, report or moderation tool.
- V3 explicitly listed chat, social graph and matchmaking as non-goals.
- V4 family invitations exist for player–NPC and can be extended to player–player.
- `PersistedWorldState` is schema 14 after feature 03.

## Deliverables

1. Presence and a bounded player directory with privacy rules.
2. Local chat and direct messages with rate limits and retention.
3. Predefined emotes with cooldowns and no free text.
4. Social graph: friendship requests, blocks, mutes and reports.
5. Consensual player partnerships built on the V4 relationship rules.
6. Shared venues and proximity activities in existing world locations.
7. Moderation queue, audit, retention, abuse controls and an admin Social page.
8. Schema 15 persistence, privacy settings and a 32-player social soak.

## Architecture

```
Player connection ──► WorldHub presence ──► PresenceRegistry
                                                │
                                                ├── nearby directory
                                                ├── friends / blocks
                                                └── venue presence
                                                        │
Chat / emotes / invites ◄── rate limit ◄── SocialService
        │                                       │
        ▼                                       ▼
Marten chat documents                  Social edges + events
(retention)                                    │
                                               ▼
                                      Admin moderation page
```

Presence and chat stay off the tick loop. Social edges that affect the simulation (player partnerships) are applied through the same tick-start command path as feature 01.

## Tech steps

| # | Step | Phase |
|---|---|---|
| 01 | [Presence and player directory](tech/01-presence-and-player-directory.md) | Foundation |
| 02 | [Chat and emotes](tech/02-chat-and-emotes.md) | Communication |
| 03 | [Social graph: friendships, blocks and reports](tech/03-social-graph-friendships-blocks-and-reports.md) | Graph |
| 04 | [Consensual player relationships](tech/04-consensual-player-relationships.md) | Relationships |
| 05 | [Shared venues and activities](tech/05-shared-venues-and-activities.md) | World |
| 06 | [Moderation, rate limits and abuse](tech/06-moderation-rate-limits-and-abuse.md) | Safety |
| 07 | [Persistence, privacy and admin](tech/07-persistence-privacy-and-admin.md) | Durability |
| 08 | [Social acceptance](tech/08-social-acceptance.md) | Verification |

## Business reading

- [Why social play](business/01-why-social-play.md)
- [Consent, safety and moderation](business/02-consent-safety-and-moderation.md)
- [Social features that fit the simulation](business/03-social-features-that-fit-the-simulation.md)

## Definition of done

- Players can see nearby players and friends without exposing private state.
- Local chat, direct messages and emotes work with rate limits, retention and mute/block.
- Friend requests, blocks and reports are persisted, audited and idempotent.
- A player partnership requires an invitation and acceptance and does not merge wallets or households automatically.
- Shared venues show presence and support a small set of proximity activities.
- Moderation can review reports, mute, block and remove content with an audit trail.
- Schema 14 → 15 migration, restore and double restore are exact; chat retention does not block restore.
- The 32-player social soak, abuse tests and reconnect tests pass.
- The admin Social page exposes presence, reports, blocks, friendships and moderation actions.
