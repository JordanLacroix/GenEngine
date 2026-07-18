using GenEngine.Identity.Application;
using GenEngine.Identity.Domain;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GenEngine.Identity.Api;

public sealed record CredentialsRequest(string UserName, string Password);
public sealed record RoleRequest(string Name, string Description, IReadOnlyList<string> Permissions);
public sealed record AssignRoleRequest(Guid RoleId, string? Scope, DateTimeOffset? ExpiresAt);
public sealed record UserStatusRequest(bool IsActive);
public sealed record AuthenticationProvidersView(
    string Mode,
    bool LocalEnabled,
    bool EntraEnabled,
    string? EntraAuthority,
    string? EntraClientId);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string code) = exception switch
        {
            IdentityDomainException domain => (StatusCodes.Status400BadRequest, domain.Code),
            IdentityException application when application.Code == "invalid_credentials" =>
                (StatusCodes.Status401Unauthorized, application.Code),
            IdentityException application => (StatusCodes.Status400BadRequest, application.Code),
            _ => (StatusCodes.Status500InternalServerError, "internal_error"),
        };

        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails
            {
                Status = status,
                Title = code,
                Detail = status == StatusCodes.Status500InternalServerError ? null : exception.Message,
            },
        }).ConfigureAwait(false);
    }
}