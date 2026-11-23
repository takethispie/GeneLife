# Session, identity and trust

A player needs an identity before the world can show them their sim. The client should treat that identity as a short-lived session, not as a login system it owns.

## What the player experiences

1. The client asks for a player id, or restores one from the current session.
2. The client exchanges that id for a JWT.
3. The JWT connects the player to the world hub.
4. When the token expires or the connection is rejected, the client renews and reconnects.

## What the client must not do

- It must not trust a player id typed into the UI as proof of ownership.
- It must not store a long-lived bearer token in `localStorage` by default.
- It must not put tokens in URLs that are logged outside the hub query string.
- It must not silently retry a rejected auth request forever.

## Trust boundary

The server owns identity, ownership, movement and interactions. The client owns presentation and intent submission. A compromised client can send bad intents, but it cannot teleport, set positions or read hidden state beyond what the server sends.

## Open decision

For the first slice, prefer an in-memory token plus `sessionStorage` fallback. Document the XSS tradeoff and provide a “sign out” path that clears both.
