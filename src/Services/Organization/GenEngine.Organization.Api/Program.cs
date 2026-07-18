using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;

using GenEngine.Observability;
using GenEngine.Organization.Api;
using GenEngine.Organization.Application;
using GenEngine.Organization.Domain;
using GenEngine.Organization.Infrastructure;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.Services.AddGenEngineObservability(builder.Configuration, "genengine-organization");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOrganizationInfrastructure(builder.Configuration);
AddJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("front.read", policy => policy.RequireAssertion(context => HasPermission(context.User, "front.read", "front.manage")));
    options.AddPolicy("front.manage", policy => policy.RequireClaim("permission", "front.manage"));
    options.AddPolicy("unit.read", policy => policy.RequireAssertion(context => HasPermission(context.User, "unit.read", "unit.manage")));
    options.AddPolicy("unit.manage", policy => policy.RequireClaim("permission", "unit.manage"));
    options.AddPolicy("period.read", policy => policy.RequireAssertion(context => HasPermission(context.User, "period.read", "period.manage")));
    options.AddPolicy("period.manage", policy => policy.RequireClaim("permission", "period.manage"));
    options.AddPolicy("membership.read", policy => policy.RequireAssertion(context => HasPermission(context.User, "membership.read", "membership.manage")));
    options.AddPolicy("membership.manage", policy => policy.RequireClaim("permission", "membership.manage"));
    options.AddPolicy("assignment.read", policy => policy.RequireAssertion(context => HasPermission(context.User, "assignment.read", "assignment.manage")));
    options.AddPolicy("assignment.manage", policy => policy.RequireClaim("permission", "assignment.manage"));
});

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (app.Configuration.GetValue<bool>("Database:AutoMigrate")) await app.Services.MigrateAndSeedOrganizationDatabaseAsync().ConfigureAwait(false);
app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapOrganizationHealthChecks();

RouteGroupBuilder admin = app.MapGroup("/admin/organization/{frontId}").RequireAuthorization();
admin.MapGet("", async (string frontId, ClaimsPrincipal actor, OrganizationService service, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    return Results.Ok(await service.GetFrontAsync(frontId, cancellationToken).ConfigureAwait(false));
}).RequireAuthorization("front.read");
admin.MapPut("", async (string frontId, UpsertFrontRequest request, ClaimsPrincipal actor, OrganizationService service, IAuditLog audit, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    FrontView result = await service.UpsertFrontAsync(frontId, request.Name, request.Type, request.IsActive, request.ExpectedRevision, cancellationToken).ConfigureAwait(false);
    RecordAudit(audit, actor, "front_upserted", "front", frontId);
    return Results.Ok(result);
}).RequireAuthorization("front.manage");
admin.MapGet("/units", async (string frontId, ClaimsPrincipal actor, OrganizationService service, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    return Results.Ok(await service.ListUnitsAsync(frontId, cancellationToken).ConfigureAwait(false));
}).RequireAuthorization("unit.read");
admin.MapPut("/units/{id:guid}", async (string frontId, Guid id, UpsertUnitRequest request, ClaimsPrincipal actor, OrganizationService service, IAuditLog audit, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    UnitView result = await service.UpsertUnitAsync(frontId, id, request.ParentId, request.Name, request.Type, request.Code, request.IsActive, request.ExpectedRevision, cancellationToken).ConfigureAwait(false);
    RecordAudit(audit, actor, "unit_upserted", "unit", id.ToString());
    return Results.Ok(result);
}).RequireAuthorization("unit.manage");
admin.MapGet("/periods", async (string frontId, ClaimsPrincipal actor, OrganizationService service, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    return Results.Ok(await service.ListPeriodsAsync(frontId, cancellationToken).ConfigureAwait(false));
}).RequireAuthorization("period.read");
admin.MapPut("/periods/{id:guid}", async (string frontId, Guid id, UpsertPeriodRequest request, ClaimsPrincipal actor, OrganizationService service, IAuditLog audit, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    PeriodView result = await service.UpsertPeriodAsync(frontId, id, request.Name, request.Code, request.StartsAt, request.EndsAt, request.IsActive, request.ExpectedRevision, cancellationToken).ConfigureAwait(false);
    RecordAudit(audit, actor, "period_upserted", "operating_period", id.ToString());
    return Results.Ok(result);
}).RequireAuthorization("period.manage");
admin.MapGet("/memberships", async (string frontId, Guid? unitId, Guid? userId, MembershipKind? kind, int? page, int? pageSize, ClaimsPrincipal actor, OrganizationService service, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    return Results.Ok(await service.ListMembershipsAsync(frontId, unitId, userId, kind, page ?? 1, pageSize ?? 25, cancellationToken).ConfigureAwait(false));
}).RequireAuthorization("membership.read");
admin.MapPut("/memberships/{id:guid}", async (string frontId, Guid id, UpsertMembershipRequest request, ClaimsPrincipal actor, OrganizationService service, IAuditLog audit, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    MembershipView result = await service.UpsertMembershipAsync(frontId, id, request.UnitId, request.UserId, request.PeriodId, request.Kind, request.StartsAt, request.EndsAt, request.IsActive, request.ExpectedRevision, cancellationToken).ConfigureAwait(false);
    RecordAudit(audit, actor, "membership_upserted", "membership", id.ToString());
    return Results.Ok(result);
}).RequireAuthorization("membership.manage");
admin.MapPost("/memberships/import", async (string frontId, ImportMembershipsRequest request, ClaimsPrincipal actor, OrganizationService service, IAuditLog audit, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    MembershipImportView result = await service.ImportMembershipsAsync(frontId, request.Rows, request.DryRun, cancellationToken).ConfigureAwait(false);
    RecordAudit(audit, actor, request.DryRun ? "membership_import_previewed" : "membership_imported", "membership_batch", result.Received.ToString(CultureInfo.InvariantCulture));
    return Results.Ok(result);
}).RequireAuthorization("membership.manage");
admin.MapDelete("/memberships/{id:guid}", async (string frontId, Guid id, ClaimsPrincipal actor, OrganizationService service, IAuditLog audit, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    await service.DeleteMembershipAsync(frontId, id, cancellationToken).ConfigureAwait(false);
    RecordAudit(audit, actor, "membership_deleted", "membership", id.ToString());
    return Results.NoContent();
}).RequireAuthorization("membership.manage");
admin.MapGet("/assignments", async (string frontId, Guid? unitId, AssignedContentType? contentType, int? page, int? pageSize, ClaimsPrincipal actor, OrganizationService service, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    return Results.Ok(await service.ListAssignmentsAsync(frontId, unitId, contentType, page ?? 1, pageSize ?? 25, cancellationToken).ConfigureAwait(false));
}).RequireAuthorization("assignment.read");
admin.MapPut("/assignments/{id:guid}", async (string frontId, Guid id, UpsertAssignmentRequest request, ClaimsPrincipal actor, OrganizationService service, IAuditLog audit, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    AssignmentView result = await service.UpsertAssignmentAsync(frontId, id, request.UnitId, request.ContentType, request.ContentId, request.Name, request.Required, request.AvailableFrom, request.DueAt, request.IsActive, request.ExpectedRevision, cancellationToken).ConfigureAwait(false);
    RecordAudit(audit, actor, "assignment_upserted", "assignment", id.ToString());
    return Results.Ok(result);
}).RequireAuthorization("assignment.manage");
admin.MapDelete("/assignments/{id:guid}", async (string frontId, Guid id, ClaimsPrincipal actor, OrganizationService service, IAuditLog audit, CancellationToken cancellationToken) =>
{
    EnsureFrontScope(actor, frontId);
    await service.DeleteAssignmentAsync(frontId, id, cancellationToken).ConfigureAwait(false);
    RecordAudit(audit, actor, "assignment_deleted", "assignment", id.ToString());
    return Results.NoContent();
}).RequireAuthorization("assignment.manage");

app.MapGet("/me/organization/{frontId}", async (string frontId, ClaimsPrincipal actor, OrganizationService service, CancellationToken cancellationToken) =>
{
    Guid userId = GetUserId(actor);
    PlayerOrganizationContextView context = await service.ResolvePlayerContextAsync(frontId, userId, cancellationToken).ConfigureAwait(false);
    return Results.Ok(context with { HasGlobalScope = actor.HasClaim("scope", "*") });
}).RequireAuthorization();

app.MapGet("/internal/access/{frontId}/users/{userId:guid}", async (string frontId, Guid userId, HttpRequest request, OrganizationService service, CancellationToken cancellationToken) =>
{
    EnsureInternalKey(request, app.Configuration);
    return Results.Ok(await service.ResolvePlayerContextAsync(frontId, userId, cancellationToken).ConfigureAwait(false));
});

app.Run();

static bool HasPermission(ClaimsPrincipal principal, params string[] permissions) => permissions.Any(permission => principal.HasClaim("permission", permission));
static Guid GetUserId(ClaimsPrincipal principal) => Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub) ?? principal.FindFirstValue(ClaimTypes.NameIdentifier) ?? throw new OrganizationException("invalid_token", "The user identifier is missing."));
static void EnsureFrontScope(ClaimsPrincipal principal, string frontId)
{
    string normalized = frontId.Trim().ToLowerInvariant();
    if (!principal.HasClaim("scope", "*") && !principal.HasClaim("scope", $"front:{normalized}"))
        throw new OrganizationException("front_scope_forbidden", "The actor is not allowed to operate on this front.");
}
static void EnsureInternalKey(HttpRequest request, IConfiguration configuration)
{
    string configured = configuration["InternalApi:Key"] ?? string.Empty;
    if (configured.Length < 24 || !request.Headers.TryGetValue("X-Internal-Key", out var supplied) || !string.Equals(configured, supplied.ToString(), StringComparison.Ordinal))
        throw new OrganizationException("internal_access_denied", "Internal access was denied.");
}
static void RecordAudit(IAuditLog audit, ClaimsPrincipal actor, string action, string resourceType, string resourceId) => audit.Record(new AuditEvent { Action = action, Outcome = AuditOutcome.Success, ActorId = GetUserId(actor).ToString(), ResourceType = resourceType, ResourceId = resourceId });
static void AddJwtAuthentication(IServiceCollection services, IConfiguration configuration)
{
    services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(options =>
    {
        string secret = configuration["Jwt:Secret"] ?? "development-only-secret-change-me-32chars";
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"] ?? "GenEngine.Identity",
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"] ?? "GenEngine.Api",
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
}

public partial class Program;