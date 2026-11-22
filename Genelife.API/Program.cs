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
                .AllowAnyMethod();
        }
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddSingleton<SimulationEngine>();
builder.Services.Configure<SnapshotStoreOptions>(builder.Configuration.GetSection(SnapshotStoreOptions.SectionName));
builder.Services.AddSingleton<ISnapshotStore, SnapshotStore>();
builder.Services.AddSingleton<SimulationManager>();
builder.Services.AddSingleton<SimulationLoop>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SimulationLoop>());
builder.Services.AddHostedService(provider => provider.GetRequiredService<SimulationManager>());
builder.Services.AddHostedService<SimulationEventWriter>();

builder.Services.AddSingleton<ICommandQueue, CommandQueue>();
builder.Services.AddSingleton<Genelife.Api.Snapshots.ReplicationTracker>();
builder.Services.AddSingleton<Genelife.Api.Snapshots.ReplicationMetrics>();
builder.Services.AddSingleton<WorldRestoreService>();
builder.Services.AddSingleton<WorldSaveService>();
builder.Services.AddSingleton<Genelife.Api.Handlers.Push.SimStatePushHandler>();
builder.Services.AddSingleton<Genelife.Api.Handlers.Push.CompanyStatePushHandler>();
builder.Services.AddHttpContextAccessor();
builder.Services.AddSingleton<WorldRestoreService>();
builder.Services.AddSingleton<WorldSaveService>();
builder.Services.AddHostedService<SnapshotBroadcastService>();

var featureFlags = builder.Configuration.GetSection("FeatureFlags");
var useWolverineEndpoints = featureFlags.GetValue<bool>("UseWolverineEndpoints");
var useMartenReads = featureFlags.GetValue<bool>("UseMartenReads");

// Marten document store.
var connectionString = builder.Configuration.GetConnectionString("genelife")
    ?? builder.Configuration.GetConnectionString("Postgres")
    ?? "Host=localhost;Port=5432;Database=genelife;Username=genelife;Password=genelife";

if (useMartenReads)
{
    builder.Services.AddMarten(opts =>
    {
        opts.Connection(connectionString);
        opts.DisableNpgsqlLogging = true;
        opts.AutoCreateSchemaObjects = JasperFx.AutoCreate.CreateOrUpdate;
        opts.Schema.For<Genelife.Api.Models.SimDocument>().Identity(x => x.Id);
        opts.Schema.For<Genelife.Api.Models.SimDocument>().Index(x => x.NetworkId);
        opts.Schema.For<Genelife.Api.Models.CompanyDocument>().Identity(x => x.Id);
        opts.Schema.For<Genelife.Api.Models.CompanyDocument>().Index(x => x.NetworkId);
        opts.Projections.Add<Genelife.Api.Projections.SimProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
        opts.Projections.Add<Genelife.Api.Projections.CompanyProjection>(JasperFx.Events.Projections.ProjectionLifecycle.Inline);
    })
        .UseLightweightSessions();
}

// Wolverine with in-memory transport.
if (useWolverineEndpoints)
{
    builder.Host.UseWolverine(opts =>
    {
        opts.Discovery.IncludeAssembly(typeof(Program).Assembly);
        opts.Discovery.IncludeAssembly(typeof(Genelife.Messages.Commands.CreateSim).Assembly);
        opts.Durability.Mode = Wolverine.DurabilityMode.ServerlessWithOutbox;
    });
    builder.Services.AddWolverineHttp();
}

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

app.MapAuthEndpoints();
app.MapSimulationEndpoints();
app.MapWorldDataEndpoints();

if (useWolverineEndpoints)
{
    app.MapWolverineEndpoints();
}

app.MapHub<WorldHub>("/hubs/world");

app.MapDefaultEndpoints();

app.Run();

public partial class Program { }
