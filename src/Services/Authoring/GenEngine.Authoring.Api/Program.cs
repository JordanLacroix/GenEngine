using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

using GenEngine.Authoring.Api;
using GenEngine.Authoring.Application;
using GenEngine.Authoring.Domain;
using GenEngine.Authoring.Infrastructure;
using GenEngine.Narrative;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow;
});
builder.Services.AddAuthoringInfrastructure(builder.Configuration);
AddJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddAuthorization();

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await app.Services.MigrateAuthoringDatabaseAsync().ConfigureAwait(false);
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapAuthoringHealthChecks();

RouteGroupBuilder scenarios = app.MapGroup("/scenarios").RequireAuthorization();

scenarios.MapPost("/import", async (
    JsonElement document,
    ClaimsPrincipal user,
    AuthoringService service,
    CancellationToken cancellationToken) =>
{
    ScenarioView result = await service.ImportAsync(
        GetUserId(user),
        document.GetRawText(),
        cancellationToken).ConfigureAwait(false);
    return Results.Created($"/scenarios/{result.Id}", result);
});

scenarios.MapGet("/{id:guid}", async (
    Guid id,
    ClaimsPrincipal user,
    AuthoringService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAsync(id, GetUserId(user), cancellationToken).ConfigureAwait(false)));

scenarios.MapPut("/{id:guid}/draft", async (
    Guid id,
    UpdateDraftRequest request,
    ClaimsPrincipal user,
    AuthoringService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateDraftAsync(
        id,
        GetUserId(user),
        request.ExpectedRevision,
        request.Document.GetRawText(),
        cancellationToken).ConfigureAwait(false)));

scenarios.MapPost("/{id:guid}/validate", async (
    Guid id,
    ClaimsPrincipal user,
    AuthoringService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.ValidateAsync(id, GetUserId(user), cancellationToken).ConfigureAwait(false)));

scenarios.MapPost("/{id:guid}/publish", async (
    Guid id,
    PublishRequest request,
    ClaimsPrincipal user,
    AuthoringService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.PublishAsync(
        id,
        GetUserId(user),
        request.ExpectedRevision,
        cancellationToken).ConfigureAwait(false)));

scenarios.MapGet("/{id:guid}/versions", async (
    Guid id,
    ClaimsPrincipal user,
    AuthoringService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.ListVersionsAsync(id, GetUserId(user), cancellationToken).ConfigureAwait(false)));

app.MapGet("/internal/scenario-versions/{versionId:guid}", async (
    Guid versionId,
    HttpRequest request,
    AuthoringService service,
    CancellationToken cancellationToken) =>
{
    string configuredKey = app.Configuration["InternalApi:Key"] ?? string.Empty;
    if (configuredKey.Length < 16
        || !request.Headers.TryGetValue("X-Internal-Key", out Microsoft.Extensions.Primitives.StringValues suppliedKey)
        || !string.Equals(configuredKey, suppliedKey.ToString(), StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    PublishedSnapshot snapshot = await service.GetPublishedSnapshotAsync(versionId, cancellationToken)
        .ConfigureAwait(false);
    return Results.Ok(snapshot);
});

app.Run();

static string GetUserId(ClaimsPrincipal user) =>
    user.FindFirstValue(JwtRegisteredClaimNames.Sub)
    ?? user.FindFirstValue(ClaimTypes.NameIdentifier)
    ?? throw new AuthoringException("unauthorized", "The authenticated user identifier is missing.");

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