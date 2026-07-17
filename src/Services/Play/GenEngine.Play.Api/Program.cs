using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

using GenEngine.Observability;
using GenEngine.Play.Api;
using GenEngine.Play.Application;
using GenEngine.Play.Infrastructure;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddGenEngineObservability(builder.Configuration, "genengine-play");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddPlayInfrastructure(builder.Configuration);
AddJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddAuthorization();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await app.Services.MigratePlayDatabaseAsync().ConfigureAwait(false);
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapPlayHealthChecks();

RouteGroupBuilder sessions = app.MapGroup("/sessions").RequireAuthorization();

sessions.MapPost("/", async (
    StartSessionRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string actorId = GetUserId(user);
    SessionView session = await service.StartAsync(
        actorId,
        request.ScenarioVersionId,
        request.Seed,
        cancellationToken).ConfigureAwait(false);
    auditLog.Record(new AuditEvent
    {
        Action = "session_started",
        Outcome = AuditOutcome.Success,
        ActorId = actorId,
        ResourceType = "session",
        ResourceId = session.Id.ToString(),
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["scenario_version_id"] = session.ScenarioVersionId.ToString(),
        },
    });
    return Results.Created($"/sessions/{session.Id}", session);
});

sessions.MapGet("/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    PlayService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAsync(id, GetUserId(user), cancellationToken).ConfigureAwait(false)));

sessions.MapGet("/{id:guid}/current-step", async (
    Guid id,
    ClaimsPrincipal user,
    PlayService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetCurrentStepAsync(id, GetUserId(user), cancellationToken).ConfigureAwait(false)));

sessions.MapPost("/{id:guid}/inputs", async (
    Guid id,
    SubmitChoiceRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string actorId = GetUserId(user);
    InputResult result = await service.SubmitChoiceAsync(
        id,
        actorId,
        request.CommandId,
        request.ExpectedRevision,
        request.ChoiceId,
        cancellationToken).ConfigureAwait(false);
    auditLog.Record(new AuditEvent
    {
        Action = result.Replayed ? "choice_replayed" : "choice_submitted",
        Outcome = AuditOutcome.Success,
        ActorId = actorId,
        ResourceType = "session",
        ResourceId = id.ToString(),
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["command_id"] = request.CommandId.ToString(),
        },
    });
    return Results.Ok(result);
});

sessions.MapPost("/{id:guid}/pause", async (
    Guid id,
    RevisionRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string actorId = GetUserId(user);
    SessionView session = await service.PauseAsync(
        id,
        actorId,
        request.ExpectedRevision,
        cancellationToken).ConfigureAwait(false);
    auditLog.Record(new AuditEvent
    {
        Action = "session_paused",
        Outcome = AuditOutcome.Success,
        ActorId = actorId,
        ResourceType = "session",
        ResourceId = id.ToString(),
    });
    return Results.Ok(session);
});

sessions.MapPost("/{id:guid}/resume", async (
    Guid id,
    RevisionRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string actorId = GetUserId(user);
    SessionView session = await service.ResumeAsync(
        id,
        actorId,
        request.ExpectedRevision,
        cancellationToken).ConfigureAwait(false);
    auditLog.Record(new AuditEvent
    {
        Action = "session_resumed",
        Outcome = AuditOutcome.Success,
        ActorId = actorId,
        ResourceType = "session",
        ResourceId = id.ToString(),
    });
    return Results.Ok(session);
});

app.Run();

static string GetUserId(ClaimsPrincipal user) =>
    user.FindFirstValue(JwtRegisteredClaimNames.Sub)
    ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? throw new PlayException("unauthorized", "The authenticated user identifier is missing.");

static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
{
    string secret = configuration["Jwt:Secret"] ?? "development-only-secret-change-me-32chars";
    string issuer = configuration["Jwt:Issuer"] ?? "GenEngine.Identity";
    string audience = configuration["Jwt:Audience"] ?? "GenEngine.Api";
    SymmetricSecurityKey key = new(Encoding.UTF8.GetBytes(secret));

    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.MapInboundClaims = false;
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = key,
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = audience,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),
                NameClaimType = JwtRegisteredClaimNames.Sub,
            };
        });
}

public partial class Program;