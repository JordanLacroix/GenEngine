using System.Text;
using System.Text.Json.Serialization;

using GenEngine.Configuration.Api;
using GenEngine.Configuration.Application;
using GenEngine.Configuration.Infrastructure;
using GenEngine.Observability;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddGenEngineObservability(builder.Configuration, "genengine-configuration");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddConfigurationInfrastructure(builder.Configuration);
builder.Services.AddAssetPacks(builder.Configuration);
AddJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("config.read", policy => policy.RequireClaim("permission", "config.read"));
    options.AddPolicy("config.write", policy => policy.RequireClaim("permission", "config.write"));
    options.AddPolicy("config.publish", policy => policy.RequireClaim("permission", "config.publish"));
    options.AddPolicy("journey.manage", policy => policy.RequireClaim("permission", "journey.manage"));
});

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await app.Services.MigrateAndSeedConfigurationDatabaseAsync().ConfigureAwait(false);
}

app.UseExceptionHandler();
// The pack bytes are public CC0 content mounted before authentication: an
// anonymous visitor of the demonstration must be able to load them, and gating
// them behind a token would make the offline journey depend on a session.
app.MapAssetPackFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapConfigurationHealthChecks();

// Anonymous projection of the published document. It carries the playable
// catalog other services consume, but never the Entra tenant and client
// identifiers, the AI provider endpoints and credential schemes, the
// organization structure or the catalog assignments. A caller holding
// config.read keeps the complete document on /admin/configuration/{frontId}.
app.MapGet("/experience/{frontId}", async (string frontId, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPublishedAsync(frontId, cancellationToken).ConfigureAwait(false)));

// Strictly minimal payload for a client that starts before any authentication:
// branding, wording, locale, authentication mode, demo flag and the public
// introduction. Nothing else — see ClientBootstrapView.
app.MapGet("/client-bootstrap/{frontId}", async (string frontId, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetClientBootstrapAsync(frontId, cancellationToken).ConfigureAwait(false)));

// AI provider connection details for a trusted backend service, guarded by the shared
// internal key exactly as Authoring guards its published-snapshot route. It carries the
// endpoint, deployment and the opaque secret reference the anonymous /experience route
// withholds — never a resolved secret. PlayerExperience reads it to reach a configured
// provider and resolve the referenced credential against its own local resolver.
app.MapGet("/internal/ai-providers/{frontId}", async (
    string frontId,
    HttpRequest request,
    ConfigurationService service,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string configuredKey = app.Configuration["InternalApi:Key"] ?? string.Empty;
    if (configuredKey.Length < 16
        || !request.Headers.TryGetValue("X-Internal-Key", out Microsoft.Extensions.Primitives.StringValues suppliedKey)
        || !string.Equals(configuredKey, suppliedKey.ToString(), StringComparison.Ordinal))
    {
        auditLog.Record(new AuditEvent
        {
            Action = "internal_ai_providers_access_denied",
            Outcome = AuditOutcome.Denied,
            ResourceType = "ai_providers",
            ResourceId = frontId,
        });
        return Results.Unauthorized();
    }

    return Results.Ok(await service.GetPublishedAiProvidersAsync(frontId, cancellationToken).ConfigureAwait(false));
});

// Asset packs are read-only content shipped with the instance. They are exposed
// next to the published experience, and for the same reason: a client must be
// able to discover what an instance publishes before it holds any credential.
app.MapGet("/asset-packs", (AssetPackService service) => Results.Ok(service.List()));
app.MapGet("/asset-packs/{packId}", (string packId, AssetPackService service) => Results.Ok(service.Get(packId)));

// Per-field integrated help. It is instance-independent — it describes the schema,
// not a front — so it sits beside the admin routes rather than inside the per-front
// group, and both clients read the same sentences instead of writing their own.
app.MapGet("/admin/configuration/field-descriptors", () => Results.Ok(ConfigurationFieldCatalog.Descriptors))
    .RequireAuthorization("config.read");

RouteGroupBuilder admin = app.MapGroup("/admin/configuration/{frontId}");
admin.MapGet("", async (string frontId, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAdminAsync(frontId, cancellationToken).ConfigureAwait(false))).RequireAuthorization("config.read");
admin.MapPut("", async (string frontId, UpdateConfigurationRequest request, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpsertAsync(frontId, request.ExpectedRevision, request.Document, cancellationToken).ConfigureAwait(false))).RequireAuthorization("config.write");
admin.MapPost("/publish", async (string frontId, PublishConfigurationRequest request, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.PublishAsync(frontId, request.ExpectedRevision, cancellationToken).ConfigureAwait(false))).RequireAuthorization("config.publish");

// Journeys are owned by the configuration document, so their operator view lives here
// rather than in PlayerExperience. It stays read-only on purpose: the Studio and the
// Administration already edit this document through PUT /admin/configuration/{frontId},
// and a second write path would make two clients race the same optimistic revision.
app.MapGet("/admin/journeys/{frontId}", async (string frontId, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetJourneyCatalogAsync(frontId, cancellationToken).ConfigureAwait(false))).RequireAuthorization("journey.manage");

app.Run();

static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
{
    string secret = configuration["Jwt:Secret"] ?? "development-only-secret-change-me-32chars";
    string issuer = configuration["Jwt:Issuer"] ?? "GenEngine.Identity";
    string audience = configuration["Jwt:Audience"] ?? "GenEngine.Api";
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = issuer,
            ValidateAudience = true,
            ValidAudience = audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
}

public partial class Program;