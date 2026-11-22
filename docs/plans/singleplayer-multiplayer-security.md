# Singleplayer and Dedicated Multiplayer Security

**Status:** Design proposal
**Depends:** Current JWT auth, role policies, and the runtime admin-secret flow.
**Related:** `docs/plans/multiplayer-v1/07-identity-and-roles.md`
**Goal:** Use one backend for local singleplayer and dedicated-server multiplayer without exposing privileged admin controls to players.

## Principle

The singleplayer/dedicated distinction changes **defaults and exposure**, not the core admin code. Treat the admin surface as local-first, then make dedicated mode opt into stricter behavior.

- **Singleplayer:** API and admin UI run on the same machine, bound to loopback. The local user is the operator, so a local-only admin path is acceptable.
- **Dedicated:** players are untrusted. The admin UI is not public by default, the admin secret stays server-side, and admin actions are audited and rate-limited.
- **Remote admin is a separate decision.** A BFF/OIDC gateway is required only when operators access the admin UI over an untrusted network.

## Threat model

| Concern | Local singleplayer | Dedicated multiplayer |
|---|---|---|
| Network boundary | Loopback, trusted | Public/private network, untrusted players |
| Admin UI | Local, safe to bundle | Must not be public by default |
| Admin secret | Local-only convenience | Server-side secret only; never shipped to clients |
| Start/stop | Local operator action | Privileged operation; audit and rate-limit |
| `/api/auth/token` | Fine as a local bootstrap | Restrict; players must not mint admin tokens |
| BFF | Unnecessary | Needed only for remote admin access |

The primary risk in dedicated mode is not the shared secret itself; it is exposing the admin surface on the public game port. Keep the player and admin surfaces separate, even when they run in the same process.

## Deployment modes and defaults

Add a deployment mode that drives configuration defaults:

| Setting | SinglePlayer | DedicatedServer |
|---|---|---|
| `Hosting:Mode` | `SinglePlayer` | `DedicatedServer` |
| `Hosting:EnableAdminUi` | `true` | `false` |
| `Hosting:AllowLocalAdminWithoutSecret` | `true` | `false` |
| `Hosting:RequireAdminSecret` | `false` | `true` |
| Web `AdminAuthMode` | `Local` | `Prompt` (local operator) or `Bff` (remote) |
| Listener | `127.0.0.1` | Public for game API; admin only on loopback/private interface |

## Implementation

### 1. Hosting options

Create `Genelife.API/Hosting/HostingOptions.cs`:

```csharp
public enum DeploymentMode
{
    SinglePlayer,
    DedicatedServer
}

public sealed class HostingOptions
{
    public const string SectionName = "Hosting";

    public DeploymentMode Mode { get; set; } = DeploymentMode.SinglePlayer;

    // Whether this process serves or bundles the admin UI.
    public bool EnableAdminUi { get; set; } = true;

    // Singleplayer convenience: loopback callers can obtain an admin token.
    public bool AllowLocalAdminWithoutSecret { get; set; } = true;

    // Dedicated: admin tokens only via the server-held secret.
    public bool RequireAdminSecret { get; set; } = false;
}
```

Register it in `Genelife.API/Program.cs`:

```csharp
builder.Services.Configure<HostingOptions>(
    builder.Configuration.GetSection(HostingOptions.SectionName));
```

Base configuration in `Genelife.API/appsettings.json`:

```json
"Hosting": {
  "Mode": "SinglePlayer",
  "EnableAdminUi": true,
  "AllowLocalAdminWithoutSecret": true,
  "RequireAdminSecret": false
}
```

Dedicated configuration comes from environment variables, not a committed file:

```text
Hosting__Mode=DedicatedServer
Hosting__EnableAdminUi=false
Hosting__AllowLocalAdminWithoutSecret=false
Hosting__RequireAdminSecret=true
Auth__Jwt__AdminSecret=<strong random value>
```

For local development, put the same keys in a gitignored `.env` file at the repository root. `Genelife.API` loads `.env` from its content root or a parent directory; existing environment variables take precedence. See `.env.example`.

The local admin UI obtains its token from the loopback-only `/api/auth/local-admin` endpoint, so the browser never receives `AdminSecret`. The secret in `.env` is used by the API for normal admin-token issuance and other admin tooling.

Fail fast at startup when `DedicatedServer` requires an admin secret but `Auth:Jwt:AdminSecret` is missing.

### 2. Singleplayer local admin token

Add a loopback-only endpoint in `Genelife.API/Endpoints/AuthEndpoints.cs`:

```csharp
app.MapPost("/api/auth/local-admin", (
    HttpContext context,
    JwtTokenService tokens,
    IOptions<HostingOptions> hosting) =>
{
    var options = hosting.Value;

    if (options.Mode != DeploymentMode.SinglePlayer ||
        !options.AllowLocalAdminWithoutSecret ||
        !IPAddress.IsLoopback(context.Connection.RemoteIpAddress!))
    {
        return Results.Forbid();
    }

    var issued = tokens.CreateToken("local-admin", isAdmin: true);
    return Results.Ok(new { accessToken = issued.Token, expiresAt = issued.ExpiresAt });
}).AllowAnonymous();
```

This removes the secret prompt for local singleplayer while remaining unavailable to remote callers. Keep `/api/auth/token` for normal player tokens; the AdminSecret path remains for dedicated/local-operator use.

If the API sits behind a reverse proxy, only trust forwarded headers when the proxy is on loopback, otherwise the remote address can be spoofed.

### 3. Dedicated hardening

In dedicated mode:

- Do not serve or bundle the admin UI by default. Set `Hosting:EnableAdminUi=false`.
- Keep the game/player API on the public listener.
- Keep `AdminSecret` only in server config (environment variables, secret manager). Never in `wwwroot` or client config.
- Add rate limiting to `/api/auth/token` and any login endpoint:

```csharp
builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("auth", limiter =>
    {
        limiter.Window = TimeSpan.FromMinutes(1);
        limiter.PermitLimit = 10;
    });
});

app.UseRateLimiter();

app.MapPost("/api/auth/token", ...).RequireRateLimiting("auth");
```

- Audit admin actions. Log `playerId`, action, result, and timestamp for `/api/simulation/start` and `/api/simulation/stop`.
- Optionally guard admin paths by network location:

```csharp
app.Use(async (context, next) =>
{
    var isAdminPath =
        context.Request.Path.StartsWithSegments("/api/simulation") ||
        context.Request.Path.StartsWithSegments("/api/sims") ||
        context.Request.Path.StartsWithSegments("/api/auth/local-admin");

    if (isAdminPath && !IPAddress.IsLoopback(context.Connection.RemoteIpAddress!))
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        return;
    }

    await next();
});
```

The Admin JWT policy remains the primary protection. The network guard is defense in depth. Remote operators should reach the admin surface through a VPN, SSH tunnel, or BFF rather than opening admin paths publicly.

### 4. Web client auth modes

Add an `AdminAuthMode` setting in `Genelife.Web`:

```json
{
  "ApiBaseUrl": "http://localhost:5288",
  "AdminAuthMode": "Local"
}
```

Values:

- `Local`: call `/api/auth/local-admin`; no prompt.
- `Prompt`: current behavior; ask for the admin secret and keep it in memory.
- `Bff`: call same-origin `/bff` endpoints; the browser never handles an admin secret or admin token.

Move admin-token acquisition in `GameDataService` behind a strategy:

```csharp
public async Task EnsureAdminTokenAsync(CancellationToken ct)
{
    switch (authMode)
    {
        case AdminAuthMode.Local:
            await ExchangeLocalAdminAsync(ct);
            break;
        case AdminAuthMode.Prompt:
            await ExchangeAdminSecretAsync(adminSecret!, ct);
            break;
        case AdminAuthMode.Bff:
            // No-op: the BFF owns credentials.
            break;
    }
}
```

`SimulationControl` only prompts for a secret in `Prompt` mode. In `Local` mode it calls Start/Stop directly; in `Bff` mode it uses the BFF client.

Add a capabilities endpoint so the UI can select the mode without duplicating configuration:

```text
GET /api/hosting/capabilities
{
  "mode": "SinglePlayer",
  "localAdminAvailable": true,
  "adminUiEnabled": true
}
```

Return only non-sensitive capability flags, and consider restricting this endpoint to loopback.

### 5. Hosting and binding

The most important operational change is where the processes listen.

**Singleplayer**

- API: `http://127.0.0.1:5288`
- Web UI: `http://127.0.0.1:5068`
- Do not bind `0.0.0.0`.
- `AdminAuthMode=Local`.
- No BFF.

**Dedicated**

- API public endpoint: `http://0.0.0.0:5288` (behind TLS in real deployments).
- Admin UI: not started, or bound to `127.0.0.1` and reached through an SSH tunnel/VPN.
- `AdminAuthMode=Prompt` for a local operator, or `Bff` for a remote admin panel.
- `AdminSecret` from environment/secret manager.

Update `LaunchSettings` and `Genelife.AppHost/Program.cs` to select endpoints by mode. The AppHost can read a mode and only launch the web project in singleplayer, or launch it with loopback-only endpoints in dedicated mode.

For Kestrel, prefer configuration over hardcoded addresses:

```json
"Kestrel": {
  "Endpoints": {
    "Public": { "Url": "http://0.0.0.0:5288" },
    "Admin":  { "Url": "http://127.0.0.1:5289" }
  }
}
```

Only add the Admin endpoint when the same process serves the admin UI. Otherwise keep the admin UI as a separate local process.

### 6. Optional admin port or BFF

Add this only when operators need remote admin access:

- **Admin-only port in the same process:** restrict admin paths to the admin listener, not the public one. The path middleware above is the cheap version; a cleaner version maps admin routes in a separate route group or `WebApplication` with a port/loopback filter.
- **BFF:** a new `Genelife.Bff` host that serves the WASM UI and proxies `/bff/simulation/*` to `Genelife.API`, holding `AdminSecret` and admin tokens server-side. This is the production path for a hosted remote admin panel.
- **OIDC:** replace the shared secret with real operator identities before exposing a BFF to the internet.

Do not put player traffic behind the BFF. It is an admin-surface gateway only.

## Files likely touched

- `Genelife.API/Hosting/HostingOptions.cs` (new)
- `Genelife.API/Program.cs`
- `Genelife.API/Endpoints/AuthEndpoints.cs`
- `Genelife.API/Endpoints/SimulationEndpoints.cs`
- `Genelife.API/appsettings.json`
- `Genelife.API/appsettings.Development.json`
- `Genelife.API/Properties/launchSettings.json`
- `Genelife.AppHost/Program.cs`
- `Genelife.Web/Services/GameDataService.cs`
- `Genelife.Web/Components/SimulationControl.razor`
- `Genelife.Web/wwwroot/appsettings*.json`
- `Genelife.UnitTests/Auth/AuthorizationTests.cs`
- `Genelife.UnitTests/Endpoints/Wolverine/WolverineEndpointsTests.cs`

## Phased rollout

### Phase 1 — mode plumbing and safe defaults

- [ ] Add `HostingOptions` and configuration validation.
- [ ] Bind launch profiles and AppHost to loopback in singleplayer mode.
- [ ] Add `/api/auth/local-admin` and the web `Local` auth mode.
- [ ] Dedicated defaults: admin UI disabled, local admin disabled, secret from environment.

### Phase 2 — dedicated hardening

- [ ] Add rate limiting for token issuance and admin actions.
- [ ] Add audit logging for start/stop.
- [ ] Add the admin-path loopback guard.
- [ ] Restrict or disable the dev token bootstrap where it is no longer needed.
- [ ] Document the singleplayer and dedicated deployment topologies.

### Phase 3 — remote admin, only if required

- [ ] Add a `Genelife.Bff` admin gateway.
- [ ] Add OIDC/real operator identity.
- [ ] Require HTTPS and antiforgery for cookie-based sessions.

## Acceptance criteria

- Singleplayer: Start/Stop works from the local admin UI without a secret, and remote callers cannot obtain an admin token.
- Dedicated: no admin secret is present in client-served files; the admin UI is not public by default; player tokens cannot start or stop the simulation; admin actions are audited and rate-limited.
- The existing authorization matrix still holds: unauthenticated requests are rejected, players cannot perform admin actions, and admins can use read endpoints.
- The full solution builds and the updated auth/hosting tests pass.

## Testing

- Singleplayer: loopback request to `/api/auth/local-admin` succeeds; non-loopback request fails; dedicated mode fails.
- Dedicated: missing `AdminSecret` fails startup validation or denies admin issuance.
- Admin endpoints reject player tokens and accept admin tokens.
- Admin UI disabled when `Hosting:EnableAdminUi=false`.
- Rate limiting returns `429` after the configured threshold.
- Web client selects `Local`, `Prompt`, or `Bff` mode correctly and handles a failed local-admin exchange.

## Risks and open questions

- A shared admin secret is still a shared secret. For multiple remote operators, OIDC is preferable to a secret prompt.
- Loopback detection can be affected by containers, reverse proxies, and forwarded headers. Do not trust forwarded headers unless the proxy is verified.
- Serving the WASM admin UI and API from the same process simplifies same-origin cookies later but mixes concerns; the BFF option keeps that separation explicit.
- Dedicated server operators may prefer a CLI or process-manager lifecycle for start/stop instead of a web button. Keep the admin surface optional.
