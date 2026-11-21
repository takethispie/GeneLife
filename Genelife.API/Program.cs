using Genelife.Api.Auth;
using Genelife.Api.Services;
using Genelife.Api.Endpoints;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register SimulationManager as singleton
builder.Services.AddSingleton<SimulationManager>();
builder.Services.AddHostedService(provider => provider.GetRequiredService<SimulationManager>());

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

var app = builder.Build();
app.UseSwagger();
app.UseSwaggerUI();
app.UseHttpsRedirection();
app.UseAuthentication();
app.UseAuthorization();

app.MapAuthEndpoints();
app.MapSimulationEndpoints();
app.MapCompanyEndpoints();

app.Run();

public partial class Program { }
