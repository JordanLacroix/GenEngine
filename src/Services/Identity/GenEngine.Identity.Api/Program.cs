using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Threading.RateLimiting;

using GenEngine.Identity.Api;
using GenEngine.Identity.Application;
using GenEngine.Identity.Infrastructure;
using GenEngine.Observability;

using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddGenEngineObservability(builder.Configuration, "genengine-identity");
builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
AddAuthentication(builder.Services, builder.Configuration);
builder.Services.AddAuthorization(options =>
    options.AddPolicy("rbac.manage", policy => policy.RequireClaim("permission", "rbac.manage")));
builder.Services.AddRateLimiter(options =>
{
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.AddPolicy("auth", context => RateLimitPartition.GetFixedWindowLimiter(
        context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        static _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromMinutes(1),
            QueueLimit = 0,
        }));
});

WebApplication app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await app.Services.MigrateIdentityDatabaseAsync().ConfigureAwait(false);
}

app.UseExceptionHandler();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapIdentityHealthChecks();

RouteGroupBuilder auth = app.MapGroup("/auth").RequireRateLimiting("auth");

auth.MapGet("/providers", (IConfiguration configuration) =>
{
    string mode = configuration["Authentication:Mode"] ?? "Cumulative";
    bool local = !string.Equals(mode, "EntraOnly", StringComparison.OrdinalIgnoreCase);
    bool entra = !string.Equals(mode, "LocalOnly", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(configuration["Authentication:Entra:TenantId"])
        && !string.IsNullOrWhiteSpace(configuration["Authentication:Entra:ClientId"]);
    string? tenantId = configuration["Authentication:Entra:TenantId"];
    return Results.Ok(new AuthenticationProvidersView(
        mode,
        local,
        entra,
        entra ? $"https://login.microsoftonline.com/{tenantId}/v2.0" : null,
        entra ? configuration["Authentication:Entra:ClientId"] : null));
});

auth.MapPost("/register", async (
    CredentialsRequest request,
    IdentityService service,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    EnsureLocalAuthenticationEnabled(app.Configuration);
    UserView user = await service.RegisterAsync(
        request.UserName,
        request.Password,
        cancellationToken).ConfigureAwait(false);
    auditLog.Record(new AuditEvent
    {
        Action = "user_registered",
        Outcome = AuditOutcome.Success,
        ActorId = user.Id.ToString(),
        ResourceType = "user",
        ResourceId = user.Id.ToString(),
    });
    return Results.Created($"/users/{user.Id}", user);
});

auth.MapPost("/login", async (
    CredentialsRequest request,
    IdentityService service,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    EnsureLocalAuthenticationEnabled(app.Configuration);
    try
    {
        AccessToken token = await service.LoginAsync(
            request.UserName,
            request.Password,
            cancellationToken).ConfigureAwait(false);
        auditLog.Record(new AuditEvent { Action = "login_succeeded", Outcome = AuditOutcome.Success });
        return Results.Ok(token);
    }
    catch (IdentityException exception) when (exception.Code == "invalid_credentials")
    {
        auditLog.Record(new AuditEvent { Action = "login_failed", Outcome = AuditOutcome.Failure });
        throw;
    }
});

auth.MapPost("/entra/exchange", async (
    ClaimsPrincipal principal,
    IdentityService service,
    IAuditLog auditLog,
    CancellationToken cancellationToken) =>
{
    EnsureEntraAuthenticationEnabled(app.Configuration);
    string subject = principal.FindFirstValue("oid")
        ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new IdentityException("invalid_external_identity", "The Entra subject is missing.");
    string userName = principal.FindFirstValue("preferred_username")
        ?? principal.FindFirstValue(ClaimTypes.Email)
        ?? principal.FindFirstValue("name")
        ?? $"entra-{subject[..Math.Min(subject.Length, 12)]}";
    AccessToken token = await service.ExchangeExternalAsync("entra", subject, userName, cancellationToken)
        .ConfigureAwait(false);
    auditLog.Record(new AuditEvent { Action = "entra_login_succeeded", Outcome = AuditOutcome.Success, ActorId = subject });
    return Results.Ok(token);
}).RequireAuthorization(new Microsoft.AspNetCore.Authorization.AuthorizeAttribute
{
    AuthenticationSchemes = "Entra",
});

app.MapGet("/me", async (
    ClaimsPrincipal principal,
    IdentityService service,
    CancellationToken cancellationToken) =>
{
    Guid userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? principal.FindFirstValue(ClaimTypes.NameIdentifier)
        ?? throw new IdentityException("invalid_token", "The user identifier is missing."));
    return Results.Ok(await service.GetAccessAsync(userId, cancellationToken).ConfigureAwait(false));
}).RequireAuthorization();

app.MapPost("/admin/access/bootstrap", async (
    ClaimsPrincipal principal,
    HttpRequest request,
    IdentityService service,
    CancellationToken cancellationToken) =>
{
    string configuredKey = app.Configuration["Bootstrap:Key"] ?? string.Empty;
    if (configuredKey.Length < 24
        || !request.Headers.TryGetValue("X-Bootstrap-Key", out Microsoft.Extensions.Primitives.StringValues supplied)
        || !string.Equals(configuredKey, supplied.ToString(), StringComparison.Ordinal))
    {
        return Results.Unauthorized();
    }

    Guid userId = Guid.Parse(principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
        ?? throw new IdentityException("invalid_token", "The user identifier is missing."));
    await service.BootstrapAdministratorAsync(userId, cancellationToken).ConfigureAwait(false);
    return Results.NoContent();
}).RequireAuthorization();

RouteGroupBuilder access = app.MapGroup("/admin/access").RequireAuthorization("rbac.manage");
access.MapGet("/permissions", () => Results.Ok(IdentityService.ListPermissions()));
access.MapGet("/roles", async (IdentityService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.ListRolesAsync(cancellationToken).ConfigureAwait(false)));
access.MapPost("/roles", async (RoleRequest request, IdentityService service, CancellationToken cancellationToken) =>
{
    RoleView role = await service.CreateRoleAsync(
        request.Name,
        request.Description,
        request.Permissions,
        cancellationToken).ConfigureAwait(false);
    return Results.Created($"/admin/access/roles/{role.Id}", role);
});
access.MapPut("/roles/{roleId:guid}", async (
    Guid roleId,
    RoleRequest request,
    IdentityService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.UpdateRoleAsync(
        roleId,
        request.Name,
        request.Description,
        request.Permissions,
        cancellationToken).ConfigureAwait(false)));
access.MapPost("/users/{userId:guid}/roles", async (
    Guid userId,
    AssignRoleRequest request,
    IdentityService service,
    CancellationToken cancellationToken) =>
{
    await service.AssignRoleAsync(
        userId,
        request.RoleId,
        request.Scope,
        request.ExpiresAt,
        cancellationToken).ConfigureAwait(false);
    return Results.NoContent();
});

app.Run();

static void AddAuthentication(IServiceCollection services, IConfiguration configuration)
{
    string secret = configuration["Jwt:Secret"] ?? "development-only-secret-change-me-32chars";
    string issuer = configuration["Jwt:Issuer"] ?? "GenEngine.Identity";
    string audience = configuration["Jwt:Audience"] ?? "GenEngine.Api";
    Microsoft.AspNetCore.Authentication.AuthenticationBuilder authentication =
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
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

    string? tenantId = configuration["Authentication:Entra:TenantId"];
    string? clientId = configuration["Authentication:Entra:ClientId"];
    if (!string.IsNullOrWhiteSpace(tenantId) && !string.IsNullOrWhiteSpace(clientId))
    {
        authentication.AddJwtBearer("Entra", options =>
        {
            options.MapInboundClaims = false;
            options.Authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
            options.Audience = clientId;
            options.TokenValidationParameters.ValidateIssuer = true;
        });
    }
}

static void EnsureLocalAuthenticationEnabled(IConfiguration configuration)
{
    if (string.Equals(configuration["Authentication:Mode"], "EntraOnly", StringComparison.OrdinalIgnoreCase))
    {
        throw new IdentityException("provider_disabled", "Local authentication is disabled.");
    }
}

static void EnsureEntraAuthenticationEnabled(IConfiguration configuration)
{
    if (string.Equals(configuration["Authentication:Mode"], "LocalOnly", StringComparison.OrdinalIgnoreCase)
        || string.IsNullOrWhiteSpace(configuration["Authentication:Entra:TenantId"])
        || string.IsNullOrWhiteSpace(configuration["Authentication:Entra:ClientId"]))
    {
        throw new IdentityException("provider_disabled", "Microsoft Entra ID authentication is disabled.");
    }
}

public partial class Program;