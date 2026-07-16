using System.Text.Json;

using GenEngine.Authoring.Application;
using GenEngine.Authoring.Domain;
using GenEngine.Narrative;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GenEngine.Authoring.Api;

public sealed record UpdateDraftRequest(int ExpectedRevision, JsonElement Document);

public sealed record PublishRequest(int ExpectedRevision);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string code) = exception switch
        {
            AuthoringDomainException domain when domain.Code == "revision_conflict" =>
                (StatusCodes.Status409Conflict, domain.Code),
            AuthoringException application when application.Code == "revision_conflict" =>
                (StatusCodes.Status409Conflict, application.Code),
            AuthoringException application when application.Code is "scenario_not_found" or "version_not_found" =>
                (StatusCodes.Status404NotFound, application.Code),
            AuthoringException application => (StatusCodes.Status422UnprocessableEntity, application.Code),
            NarrativeException narrative => (StatusCodes.Status422UnprocessableEntity, narrative.Code),
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