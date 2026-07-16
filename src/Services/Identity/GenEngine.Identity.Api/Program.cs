using System.Threading.RateLimiting;

using GenEngine.Identity.Api;
using GenEngine.Identity.Application;
using GenEngine.Identity.Infrastructure;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddIdentityInfrastructure(builder.Configuration);
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
app.MapIdentityHealthChecks();

RouteGroupBuilder auth = app.MapGroup("/auth").RequireRateLimiting("auth");

auth.MapPost("/register", async (
    CredentialsRequest request,
    IdentityService service,
    CancellationToken cancellationToken) =>
{
    UserView user = await service.RegisterAsync(
        request.UserName,
        request.Password,
        cancellationToken).ConfigureAwait(false);
    return Results.Created($"/users/{user.Id}", user);
});

auth.MapPost("/login", async (
    CredentialsRequest request,
    IdentityService service,
    CancellationToken cancellationToken) =>
    Results.Ok(await service.LoginAsync(
        request.UserName,
        request.Password,
        cancellationToken).ConfigureAwait(false)));

app.Run();

public partial class Program;