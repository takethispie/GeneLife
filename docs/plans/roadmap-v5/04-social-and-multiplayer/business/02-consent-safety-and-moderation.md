# Consent, safety and moderation

Any feature that lets players talk to each other needs a safety model before it needs a feature list. V5 ships moderation with chat, not after an incident.

## Consent

- A friendship requires a request and an acceptance.
- A player partnership requires an invitation and an acceptance, using the feature 01 invitation path.
- Proximity never creates a relationship.
- A declined or expired invitation creates no edge and starts a cooldown.
- A block prevents chat, invitations and presence details in both directions.

## Safety

- Text chat is rate-limited and length-limited.
- Emotes are predefined; there is no free-text emote channel.
- Direct messages are limited to friends or explicit acceptance.
- Chat history has a retention window and is not part of the world snapshot.
- Reports create a moderation item with the message, reporter, target, tick and reason.
- Mute hides a player's chat without removing them from the world.
- Block hides chat and invitations and prevents the blocked player from targeting the blocker.

## Moderation

- The admin Social page shows reports, blocks, mutes, friendships and recent chat.
- A moderator can mute, block, remove a message and record a decision.
- Moderation actions are audited and reversible where possible.
- Retention and deletion rules are documented before launch.
- There is an appeal note path, even if the first version is manual.

## Privacy

- No real names, emails, IP addresses or external account ids in world state or read models.
- The player id is the identity; a display name is optional and moderated.
- Player routes expose only public presence and the player's own private state.
- Family, wallet, utility and trait data are never part of the public directory.

## What not to ship

- Global chat with no moderation.
- A public profile with family or financial details.
- A report button that creates a ticket nobody reads.
- A block that only hides the UI but still delivers messages.
- Chat history that makes a world restore fail.

## Success signal

A player can meet someone, talk, add a friend, decline an invitation, block an abuser and report a message, and the operator can see and act on the report without reading the world snapshot.
