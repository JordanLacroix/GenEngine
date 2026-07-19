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
builder.Services.AddAuthorization(options =>
    options.AddPolicy("session.play", policy => policy.RequireClaim("permission", "session.play")));

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

RouteGroupBuilder sessions = app.MapGroup("/sessions").RequireAuthorization("session.play");

// Narrative map of a published version, consultable outside any session.
app.MapGet("/scenario-versions/{versionId:guid}/tree", async (
    Guid versionId,
    ClaimsPrincipal user,
    PlayService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetStructureAsync(
        GetUserId(user),
        versionId,
        user.HasClaim("scope", "*"),
        cancellationToken).ConfigureAwait(false)))
    .RequireAuthorization("session.play");

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
        user.HasClaim("scope", "*"),
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

sessions.MapGet("/{id:guid}/tree", async (
    Guid id,
    ClaimsPrincipal user,
    PlayService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetTreeAsync(id, GetUserId(user), cancellationToken).ConfigureAwait(false)));

sessions.MapGet("/{id:guid}/player", async (
    Guid id,
    ClaimsPrincipal user,
    PlayService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPlayerProjectionAsync(id, GetUserId(user), cancellationToken).ConfigureAwait(false)));

sessions.MapPost("/{id:guid}/inputs", async (
    Guid id,
    SubmitChoiceRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IRewardDispatcher rewards,
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
    await rewards.DispatchAsync(actorId, result.Session.FrontId, result.Rewards, cancellationToken).ConfigureAwait(false);
    await rewards.DispatchProgressAsync(actorId, result.Session.FrontId, result.Progress, cancellationToken).ConfigureAwait(false);
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

sessions.MapPost("/{id:guid}/continue", async (
    Guid id,
    ContinueInteractionRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IRewardDispatcher rewards,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string actorId = GetUserId(user);
    InputResult result = await service.ContinueAsync(
        id,
        actorId,
        request.CommandId,
        request.ExpectedRevision,
        cancellationToken).ConfigureAwait(false);
    await rewards.DispatchAsync(actorId, result.Session.FrontId, result.Rewards, cancellationToken).ConfigureAwait(false);
    await rewards.DispatchProgressAsync(actorId, result.Session.FrontId, result.Progress, cancellationToken).ConfigureAwait(false);
    RecordInputAudit(auditLog, actorId, id, request.CommandId, result.Replayed, "narration_continued");
    return Results.Ok(result);
});

sessions.MapPost("/{id:guid}/answers", async (
    Guid id,
    SubmitAnswerRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IRewardDispatcher rewards,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string actorId = GetUserId(user);
    InputResult result = await service.SubmitAnswerAsync(
        id,
        actorId,
        request.CommandId,
        request.ExpectedRevision,
        request.AnswerId,
        cancellationToken).ConfigureAwait(false);
    await rewards.DispatchAsync(actorId, result.Session.FrontId, result.Rewards, cancellationToken).ConfigureAwait(false);
    await rewards.DispatchProgressAsync(actorId, result.Session.FrontId, result.Progress, cancellationToken).ConfigureAwait(false);
    RecordInputAudit(auditLog, actorId, id, request.CommandId, result.Replayed, "quiz_answered");
    return Results.Ok(result);
});

sessions.MapPost("/{id:guid}/text-inputs", async (
    Guid id,
    SubmitTextRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IRewardDispatcher rewards,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string actorId = GetUserId(user);
    InputResult result = await service.SubmitTextAsync(
        id,
        actorId,
        request.CommandId,
        request.ExpectedRevision,
        request.Text,
        cancellationToken).ConfigureAwait(false);
    await rewards.DispatchAsync(actorId, result.Session.FrontId, result.Rewards, cancellationToken).ConfigureAwait(false);
    await rewards.DispatchProgressAsync(actorId, result.Session.FrontId, result.Progress, cancellationToken).ConfigureAwait(false);
    RecordInputAudit(auditLog, actorId, id, request.CommandId, result.Replayed, "text_submitted");
    return Results.Ok(result);
});

sessions.MapPost("/{id:guid}/text-inputs/confirm", async (
    Guid id,
    ConfirmTextAnalysisRequest request,
    ClaimsPrincipal user,
    PlayService service,
    IRewardDispatcher rewards,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    string actorId = GetUserId(user);
    InputResult result = await service.ConfirmTextAnalysisAsync(
        id,
        actorId,
        request.CommandId,
        request.ExpectedRevision,
        request.Confirmed,
        cancellationToken).ConfigureAwait(false);
    await rewards.DispatchAsync(actorId, result.Session.FrontId, result.Rewards, cancellationToken).ConfigureAwait(false);
    await rewards.DispatchProgressAsync(actorId, result.Session.FrontId, result.Progress, cancellationToken).ConfigureAwait(false);
    RecordInputAudit(auditLog, actorId, id, request.CommandId, result.Replayed, "text_analysis_confirmed");
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

static void RecordInputAudit(
    IAuditLog auditLog,
    string actorId,
    Guid sessionId,
    Guid commandId,
    bool replayed,
    string action)
{
    auditLog.Record(new AuditEvent
    {
        Action = replayed ? $"{action}_replayed" : action,
        Outcome = AuditOutcome.Success,
        ActorId = actorId,
        ResourceType = "session",
        ResourceId = sessionId.ToString(),
        Properties = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["command_id"] = commandId.ToString(),
        },
    });
}

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