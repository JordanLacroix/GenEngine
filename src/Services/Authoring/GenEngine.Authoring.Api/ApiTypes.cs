using System.Text.Json;

using GenEngine.Authoring.Application;
using GenEngine.Authoring.Domain;
using GenEngine.Narrative;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GenEngine.Authoring.Api;

public sealed record UpdateDraftRequest(int ExpectedRevision, JsonElement Document);

public sealed record PublishRequest(int ExpectedRevision);

public sealed record ScenarioPreviewRequest(string NodeId, int Turn = 0)
{
    public Dictionary<string, int>? Variables { get; init; }

    public Dictionary<string, int>? Characteristics { get; init; }

    public IReadOnlyList<string>? Inventory { get; init; }

    public IReadOnlyList<string>? Evidence { get; init; }

    public Dictionary<string, int>? Relations { get; init; }

    public IReadOnlyList<string>? Rewards { get; init; }

    public IReadOnlyList<string>? VisitedNodes { get; init; }
}

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