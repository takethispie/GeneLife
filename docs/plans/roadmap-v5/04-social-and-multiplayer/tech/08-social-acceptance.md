# S08 — Social acceptance

**Phase:** 8 — Verification
**Depends:** S01–S07
**Estimate:** 2 sessions
**Status:** Planned (V5)

## Goal

Prove that the social layer is safe, private, durable and usable with 32 concurrent players before V5 release.

## Acceptance scenarios

### Presence

1. Connect two players nearby and assert mutual visibility.
2. Move one away and assert the nearby scope drops it.
3. Enter a venue and assert venue presence.
4. Disconnect and assert stale expiry.
5. Reconnect and assert presence restoration.

### Chat and emotes

1. Send local, venue and whisper messages and assert recipients.
2. Exceed each rate limit and assert the drop notice.
3. Block a pair and assert no delivery.
4. Report a message and assert evidence.
5. Advance retention and assert removal.
6. Assert chat is absent from the world snapshot.

### Social graph

1. Request and accept a friendship; assert mirrored edges.
2. Decline and expire a request; assert cooldown.
3. Block and assert presence/chat/invite filtering both ways.
4. Mute and assert chat-only filtering.
5. Reconnect and assert the block holds.

### Player relationships

1. Invite and accept a player partnership; assert partner edges.
2. Decline and expire; assert no edge.
3. Accept cohabitation with capacity; assert merge.
4. Accept cohabitation without capacity; assert separate households.
5. End the partnership; assert `ExPartner` and household split.

### Venues

1. Enter a venue and assert presence and channel access.
2. Fill capacity and assert rejection.
3. Run each activity and assert cooldown and event.
4. Share a meal and assert pantry consumption.
5. Close the venue and assert rejection.

### Moderation

1. Submit duplicate reports and assert one queue item.
2. Apply and reverse each moderation action.
3. Flood chat and assert the rate limit.
4. Reconnect a blocked player and assert the block.
5. Assert evidence survives message deletion.

### Scale

1. Run a 32-player soak with presence, chat, friends, invitations and venue activity.
2. Assert no message loss inside limits, no duplicate edges and no tick-loop allocation.
3. Assert the 2,000-sim decade still meets the tick budget.

### Migration and privacy

1. Load a schema 14 fixture.
2. Migrate to 15 and assert empty social state and default settings.
3. Restore again and compare.
4. Assert no real names, emails or IP addresses are persisted.
5. Assert player routes do not expose another player's private state.

## Tasks

- [ ] Add the social acceptance suite.
- [ ] Add the 32-player soak test.
- [ ] Add the schema 14 fixture.
- [ ] Add the privacy assertion.
- [ ] Add the admin Social smoke test.
- [ ] Record results in `roadmap-v5/STATUS.md` when implemented.
- [ ] Add the social section to the release checklist.

## Files

- `Genelife.UnitTests/Social/SocialAcceptanceTests.cs`
- `Genelife.UnitTests/Performance/SocialSoakTests.cs`
- `Genelife.UnitTests/Fixtures/v14-social.json`
- `clients/browser/e2e/social.spec.ts`
- `docs/plans/roadmap-v5/STATUS.md`

## Acceptance

- Every scenario passes on a clean build.
- Presence, chat and relationships are private and consent-based.
- Blocks, mutes and reports work in both directions and survive reconnect.
- Chat retention and evidence handling are correct.
- The 32-player soak and 2,000-sim decade pass.
- No economy, family, housing or persistence test regresses.

## Testing

- Full social suite.
- Presence, chat, graph, relationship, venue and moderation suites.
- 32-player soak with 200 ticks and a full year.
- 2,000-sim decade with social activity.
- Playwright social smoke.
