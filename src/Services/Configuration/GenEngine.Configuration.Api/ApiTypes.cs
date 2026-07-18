using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GenEngine.Configuration.Api;

public sealed record UpdateConfigurationRequest(int? ExpectedRevision, ExperienceDocument Document);
public sealed record PublishConfigurationRequest(int ExpectedRevision);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string code) = exception switch
        {
            ConfigurationDomainException domain when domain.Code == "revision_conflict" => (StatusCodes.Status409Conflict, domain.Code),
            ConfigurationDomainException domain => (StatusCodes.Status400BadRequest, domain.Code),
            ConfigurationException application when application.Code == "configuration_not_found" => (StatusCodes.Status404NotFound, application.Code),
            ConfigurationException application when application.Code == "revision_conflict" => (StatusCodes.Status409Conflict, application.Code),
            ConfigurationException application => (StatusCodes.Status400BadRequest, application.Code),
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