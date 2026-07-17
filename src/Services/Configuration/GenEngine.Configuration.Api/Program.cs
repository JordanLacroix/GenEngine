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
AddJwtAuthentication(builder.Services, builder.Configuration);
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("config.read", policy => policy.RequireClaim("permission", "config.read"));
    options.AddPolicy("config.write", policy => policy.RequireClaim("permission", "config.write"));
    options.AddPolicy("config.publish", policy => policy.RequireClaim("permission", "config.publish"));
});

WebApplication app = builder.Build();
if (app.Environment.IsDevelopment()) app.MapOpenApi();
if (app.Configuration.GetValue<bool>("Database:AutoMigrate"))
{
    await app.Services.MigrateAndSeedConfigurationDatabaseAsync().ConfigureAwait(false);
}

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();
app.MapConfigurationHealthChecks();

app.MapGet("/experience/{frontId}", async (string frontId, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetPublishedAsync(frontId, cancellationToken).ConfigureAwait(false)));

RouteGroupBuilder admin = app.MapGroup("/admin/configuration/{frontId}");
admin.MapGet("", async (string frontId, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetAdminAsync(frontId, cancellationToken).ConfigureAwait(false))).RequireAuthorization("config.read");
admin.MapPut("", async (string frontId, UpdateConfigurationRequest request, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpsertAsync(frontId, request.ExpectedRevision, request.Document, cancellationToken).ConfigureAwait(false))).RequireAuthorization("config.write");
admin.MapPost("/publish", async (string frontId, PublishConfigurationRequest request, ConfigurationService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.PublishAsync(frontId, request.ExpectedRevision, cancellationToken).ConfigureAwait(false))).RequireAuthorization("config.publish");

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