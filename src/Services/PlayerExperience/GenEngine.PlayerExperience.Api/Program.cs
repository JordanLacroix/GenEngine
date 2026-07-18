using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using GenEngine.Observability;
using GenEngine.PlayerExperience.Api;
using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Infrastructure;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddGenEngineObservability(builder.Configuration, "genengine-player-experience");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddPlayerExperienceInfrastructure(builder.Configuration);
AddJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("session.play", policy => policy.RequireClaim("permission", "session.play"));
    options.AddPolicy("shop.read", policy => policy.RequireClaim("permission", "shop.read"));
    options.AddPolicy("assistant.use", policy => policy.RequireClaim("permission", "assistant.use"));
    options.AddPolicy("assistant.customize", policy => policy.RequireClaim("permission", "assistant.customize"));
    options.AddPolicy("onboarding.use", policy => policy.RequireClaim("permission", "onboarding.use"));
    options.AddPolicy("onboarding.reset.own", policy => policy.RequireClaim("permission", "onboarding.reset.own"));
    options.AddPolicy("journal.read.own", policy => policy.RequireClaim("permission", "journal.read.own"));
});

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await app.Services.MigratePlayerExperienceDatabaseAsync().ConfigureAwait(false);
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapPlayerExperienceHealthChecks();

RouteGroupBuilder experience = app.MapGroup("/me/experience").RequireAuthorization("session.play");
experience.MapGet("", async (string? frontId, ClaimsPrincipal user, PlayerExperienceService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAsync(GetUserId(user), frontId ?? "default", cancellationToken).ConfigureAwait(false)));
experience.MapGet("/bootstrap", async (string? frontId, ClaimsPrincipal user, PlayerExperienceService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetBootstrapAsync(GetUserId(user), frontId ?? "default", cancellationToken).ConfigureAwait(false)));
experience.MapPut("/familiar", async (
    string? frontId,
    ConfigureFamiliarRequest request,
    ClaimsPrincipal user,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.ConfigureFamiliarAsync(
        GetUserId(user),
        frontId ?? "default",
        request.Selection,
        request.ExpectedRevision,
        cancellationToken).ConfigureAwait(false))).RequireAuthorization("assistant.customize");
experience.MapPost("/onboarding/steps/{stepId:guid}/complete", async (
    Guid stepId,
    string? frontId,
    OnboardingCommandRequest request,
    ClaimsPrincipal user,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.CompleteOnboardingStepAsync(GetUserId(user), frontId ?? "default", stepId, request.IdempotencyKey, cancellationToken).ConfigureAwait(false)))
    .RequireAuthorization("onboarding.use");
experience.MapPost("/onboarding/skip", async (
    string? frontId,
    OnboardingCommandRequest request,
    ClaimsPrincipal user,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.SkipOnboardingAsync(GetUserId(user), frontId ?? "default", request.IdempotencyKey, cancellationToken).ConfigureAwait(false)))
    .RequireAuthorization("onboarding.use");
experience.MapPost("/onboarding/reset", async (
    string? frontId,
    ClaimsPrincipal user,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.ResetOnboardingAsync(GetUserId(user), frontId ?? "default", cancellationToken).ConfigureAwait(false)))
    .RequireAuthorization("onboarding.reset.own");
experience.MapGet("/journal", async (
    string? frontId,
    string? type,
    Guid? journeyId,
    Guid? categoryId,
    Guid? scenarioId,
    int? offset,
    int? limit,
    ClaimsPrincipal user,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetJournalAsync(GetUserId(user), frontId ?? "default", type, journeyId, categoryId, scenarioId, offset ?? 0, limit ?? 30, cancellationToken).ConfigureAwait(false)))
    .RequireAuthorization("journal.read.own");
experience.MapPost("/assistant/contextual-help", async (
    string? frontId,
    ContextualHelpRequest request,
    ClaimsPrincipal user,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetContextualHelpAsync(GetUserId(user), frontId ?? "default", request, cancellationToken).ConfigureAwait(false)))
    .RequireAuthorization("assistant.use");
experience.MapPost("/shop/purchases", async (
    string? frontId,
    PurchaseRequest request,
    ClaimsPrincipal user,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.PurchaseAsync(
        GetUserId(user),
        frontId ?? "default",
        request.OfferId,
        request.IdempotencyKey,
        cancellationToken).ConfigureAwait(false))).RequireAuthorization("shop.read");

app.MapPost("/internal/rewards", async (
    RewardCommand command,
    HttpRequest request,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
{
    string configuredKey = app.Configuration["InternalApi:Key"] ?? string.Empty;
    if (configuredKey.Length < 16
        || !request.Headers.TryGetValue("X-Internal-Key", out Microsoft.Extensions.Primitives.StringValues supplied)
        || !string.Equals(configuredKey, supplied.ToString(), StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    return Results.Ok(await service.ApplyRewardAsync(command, cancellationToken).ConfigureAwait(false));
});

app.MapPost("/internal/progress-events", async (
    ProgressEventCommand command,
    HttpRequest request,
    PlayerExperienceService service,
    CancellationToken cancellationToken) =>
{
    if (!HasValidInternalKey(app, request)) return Results.Unauthorized();
    return Results.Ok(await service.RecordProgressEventAsync(command, cancellationToken).ConfigureAwait(false));
});

app.Run();

static string GetUserId(ClaimsPrincipal user) =>
    user.FindFirstValue(JwtRegisteredClaimNames.Sub)
    ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? throw new PlayerExperienceException("unauthorized", "The authenticated user identifier is missing.");

static bool HasValidInternalKey(WebApplication app, HttpRequest request)
{
    string configuredKey = app.Configuration["InternalApi:Key"] ?? string.Empty;
    return configuredKey.Length >= 16
        && request.Headers.TryGetValue("X-Internal-Key", out Microsoft.Extensions.Primitives.StringValues supplied)
        && string.Equals(configuredKey, supplied.ToString(), StringComparison.Ordinal);
}

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
