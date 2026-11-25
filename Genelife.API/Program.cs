using Genelife.Api.Auth;
using Genelife.Api.Commands;
using Genelife.Api.Hosting;
using Genelife.Api.Hubs;
using Genelife.Api.Persistence;
using Genelife.Api.Services;
using Genelife.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;
using Wolverine.SignalR;
using Marten;

var builder = WebApplication.CreateBuilder(args);

DotEnv.Load(builder.Configuration, builder.Environment.ContentRootPath);

builder.AddServiceDefaults();

const string AdminUiCorsPolicy = "AdminUi";

builder.Services.AddCors(options =>
{
    options.AddPolicy(AdminUiCorsPolicy, policy =>
    {
        var allowedOrigins = builder.Configuration
            .GetSection("Cors:AllowedOrigins")
            .Get<string[]>();

        if (allowedOrigins is { Length: > 0 })
        {
            policy.WithOrigins(allowedOrigins)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .AllowCredentials();
        }
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SimulationEngine>();
builder.Services.AddMemoryCache();
builder.Services.AddSingleton<RequestRateLimiter>();
builder.Services.Configure<SnapshotStoreOptions>(builder.Configuration.GetSection(SnapshotStoreOptions.SectionName));
builder.Services.AddSingleton<ISnapshotStore, SnapshotStore>();
builder.Services.AddSingleton<SimulationManager>();
builder.Services.AddSingleton<SimulationLoop>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SimulationLoop>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<SimulationManager>());
builder.Services.AddHostedService<SimulationEventWriter>();

builder.Services.AddSingleton<ICommandQueue, CommandQueue>();
builder.Services.AddSingleton<Genelife.Social.SocialPrivacyRegistry>();
builder.Services.Configure<Genelife.Social.SocialGraphOptions>(builder.Configuration.GetSection("SocialGraph"));
builder.Services.Configure<Genelife.Social.VenueOptions>(builder.Configuration.GetSection("Venue"));
builder.Services.Configure<Genelife.Social.SocialRateLimitOptions>(builder.Configuration.GetSection("SocialLimits"));
builder.Services.Configure<Genelife.Social.ModerationOptions>(builder.Configuration.GetSection("Moderation"));
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Genelife.Social.VenueOptions>>().Value);
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Genelife.Social.SocialRateLimitOptions>>().Value);
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Genelife.Social.SocialGraphOptions>>().Value);
builder.Services.AddSingleton<Genelife.Social.ISocialAuditStore>(provider =>
    provider.GetService<Marten.IDocumentStore>() is { } auditDocumentStore
        ? new Genelife.Api.Social.MartenSocialAuditStore(auditDocumentStore)
        : new Genelife.Social.InMemorySocialAuditStore());
builder.Services.AddSingleton<Genelife.Social.SocialGraphService>();
builder.Services.AddSingleton<Genelife.Social.IChatSocialPolicy>(provider =>
    provider.GetRequiredService<Genelife.Social.SocialGraphService>());
builder.Services.AddSingleton<Genelife.Api.Social.PlayerDirectoryService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.Configure<Genelife.Social.ChatOptions>(builder.Configuration.GetSection(Genelife.Social.ChatOptions.SectionName));
builder.Services.AddSingleton(provider => provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Genelife.Social.ChatOptions>>().Value);
builder.Services.AddSingleton<Genelife.Social.IChatDirectory, Genelife.Api.Social.ChatDirectory>();
builder.Services.AddSingleton<Genelife.Social.IChatStore>(provider =>
    provider.GetService<Marten.IDocumentStore>() is { } documentStore
        ? new Genelife.Api.Social.MartenChatStore(documentStore)
        : new Genelife.Api.Social.InMemoryChatStore());
builder.Services.AddSingleton<Genelife.Social.ChatService>();
builder.Services.AddSingleton(provider =>
    provider.GetRequiredService<Microsoft.Extensions.Options.IOptions<Genelife.Social.ModerationOptions>>().Value);
builder.Services.AddSingleton<Genelife.Api.Social.ModerationService>();
builder.Services.AddHostedService<Genelife.Api.Social.ModerationRetentionService>();
builder.Services.AddSingleton<Genelife.Social.EmoteService>();
builder.Services.AddHostedService<Genelife.Api.Social.ChatRetentionService>();
builder.Services.AddSingleton<PlayerCommandService>();
builder.Services.AddSingleton<Genelife.Api.Snapshots.ReplicationTracker>();
builder.Services.AddSingleton<Genelife.Api.Snapshots.ReplicationMetrics>();
builder.Services.AddSingleton<Genelife.Api.Snapshots.ViewportRegistry>();
builder.Services.AddSingleton<WorldRestoreService>();
builder.Services.AddSingleton<WorldSaveService>();
builder.Services.AddSingleton<IWorldPersistence>(provider => provider.GetRequiredService<WorldSaveService>());
builder.Services.AddSingleton<IReadModelStore, MartenReadModelStore>();
builder.Services.AddSingleton<IEventConsumerProbe, MartenEventConsumerProbe>();
builder.Services.AddSingleton<Genelife.Api.Handlers.Push.SimStatePushHandler>();
builder.Services.AddSingleton<Genelife.Api.Handlers.Push.CompanyStatePushHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<SnapshotBroadcastService>();
builder.Services.AddSingleton<IWorldResetNotifier>(provider => provider.GetRequiredService<SnapshotBroadcastService>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<SnapshotBroadcastService>());

// Marten document store. Reads, projections and the event store are the only
// read/write path; there is no snapshot fallback.
var connectionString = builder.Configuration.GetConnectionString("genelife")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=genelife;Username=genelife;Password=genelife";

var martenSchema = builder.Configuration["Marten:SchemaName"];
builder.Services.AddMarten(opts =>
{
    opts.Connection(connectionString);
    if (!string.IsNullOrWhiteSpace(martenSchema))
        opts.DatabaseSchemaName = martenSchema;
    opts.DisableNpgsqlLogging = true;
    opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.CreateOrUpdate;
    opts.Schema.For<Genelife.Api.Models.SimDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.SimDocument>().Index(x => x.NetworkId);
    opts.Schema.For<Genelife.Api.Models.CompanyDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.CompanyDocument>().Index(x => x.NetworkId);
    opts.Schema.For<Genelife.Api.Models.MapDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.BuildingDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.BuildingDocument>().Index(x => x.NetworkId);
    opts.Schema.For<Genelife.Api.Models.FamilyDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.FamilyDocument>().Index(x => x.SimNetworkId);
    opts.Schema.For<Genelife.Api.Models.RelationshipDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.RelationshipDocument>().Index(x => x.RelationshipNetworkId);
    opts.Schema.For<Genelife.Api.Models.PregnancyDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.PregnancyDocument>().Index(x => x.PregnancyNetworkId);
    opts.Schema.For<Genelife.Api.Models.ChildDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.ChildDocument>().Index(x => x.ChildNetworkId);
    opts.Schema.For<Genelife.Api.Models.HouseholdCompositionDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.HouseholdCompositionDocument>().Index(x => x.HouseholdNetworkId);
    opts.Schema.For<Genelife.Api.Models.EstateDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.EstateDocument>().Index(x => x.DeceasedNetworkId);
    opts.Schema.For<Genelife.Api.Models.FamilyAdminActionDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.FamilyAdminActionDocument>().Index(x => x.CommandId);
    opts.Schema.For<Genelife.Api.Models.SocialReportDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.SocialReportDocument>().Index(x => x.CreatedTick);
    opts.Schema.For<Genelife.Api.Models.ModerationActionDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.ModerationActionDocument>().Index(x => x.CreatedTick);
    opts.Schema.For<Genelife.Api.Models.ChatMessageDocument>().Identity(x => x.Id);
    opts.Schema.For<Genelife.Api.Models.ChatMessageDocument>().Index(x => x.PlayerId);
    opts.Schema.For<Genelife.Api.Models.ChatMessageDocument>().Index(x => x.ExpiresAt);
    opts.Projections.Add<Genelife.Api.Projections.SimProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.CompanyProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.MapProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.FamilyProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.RelationshipProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.PregnancyProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.ChildProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.HouseholdCompositionProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.EstateProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    opts.Projections.Add<Genelife.Api.Projections.PlayerProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
})
    .UseLightweightSessions();

// Wolverine with in-memory transport. All HTTP endpoints go through Wolverine
// and all commands are drained from the Wolverine-backed command queue.
builder.Host.UseWolverine(opts =>
{
    opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
    opts.Discovery.IncludeAssembly(typeof(Genelife.Messages.Commands.CreateSim).Assembly);
    opts.Durability.Mode = Wolverine.DurabilityMode.ServerlessWithOutbox;
});
builder.Services.AddWolverineHttp();

// Authentication and authorization.
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.AddSingleton<JwtSigningKeyProvider>();
builder.Services.AddSingleton<JwtTokenService>();
builder.Services.AddSingleton<IConnectionRegistry, ConnectionRegistry>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer();

builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<JwtSigningKeyProvider, IOptions<JwtOptions>>((options, keyProvider, jwtOptions) =>
    {
        var jwt = jwtOptions.Value;

        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = keyProvider.Key,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
            NameClaimType = AuthClaimTypes.PlayerId,
            RoleClaimType = AuthClaimTypes.Role
        };

        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];
                var path = context.HttpContext.Request.Path;

                if (!string.IsNullOrEmpty(accessToken) && path.StartsWithSegments("/hubs"))
                    context.Token = accessToken;

                return Task.CompletedTask;
            },
            OnChallenge = async context =>
            {
                if (context.Response.HasStarted)
                    return;

                context.HandleResponse();
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                context.Response.Headers.WWWAuthenticate = "Bearer";
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "unauthorized",
                    message = "Authentication is required."
                });
            },
            OnForbidden = async context =>
            {
                if (context.Response.HasStarted)
                    return;

                context.Response.StatusCode = StatusCodes.Status403Forbidden;
                await context.Response.WriteAsJsonAsync(new
                {
                    error = "forbidden",
                    message = "You do not have permission to perform this action."
                });
            }
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Every endpoint requires an authenticated player unless it opts out
    // (for example, the token endpoint with AllowAnonymous).
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();

    options.AddPolicy(AuthPolicies.Player, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(AuthRoles.Player, AuthRoles.Admin));

    options.AddPolicy(AuthPolicies.Admin, policy => policy
        .RequireAuthenticatedUser()
        .RequireRole(AuthRoles.Admin));
});

builder.Services.AddSignalR(options =>
{
    options.MaximumReceiveMessageSize = 8 * 1024;
    options.EnableDetailedErrors = builder.Environment.IsDevelopment();
});

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();

if (!app.Environment.IsDevelopment())
{
    app.UseHttpsRedirection();
}

app.UseCors(AdminUiCorsPolicy);
app.UseAuthentication();
app.UseAuthorization();

app.MapWolverineEndpoints();

app.MapHub<WorldHub>("/hubs/world");

app.MapDefaultEndpoints();

app.Run();

public partial class Program { }
