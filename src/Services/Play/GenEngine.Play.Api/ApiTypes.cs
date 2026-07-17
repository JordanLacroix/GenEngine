using GenEngine.Narrative;
using GenEngine.Play.Application;
using GenEngine.Play.Domain;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GenEngine.Play.Api;

public sealed record StartSessionRequest(Guid ScenarioVersionId, ulong Seed);

public sealed record SubmitChoiceRequest(Guid CommandId, int ExpectedRevision, string ChoiceId);

public sealed record ContinueInteractionRequest(Guid CommandId, int ExpectedRevision);

public sealed record SubmitAnswerRequest(Guid CommandId, int ExpectedRevision, string AnswerId);

public sealed record RevisionRequest(int ExpectedRevision);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        (int status, string code) = exception switch
        {
            PlayDomainException domain when domain.Code == "revision_conflict" =>
                (StatusCodes.Status409Conflict, domain.Code),
            PlayException application when application.Code is "revision_conflict" or "command_conflict" =>
                (StatusCodes.Status409Conflict, application.Code),
            PlayException application when application.Code is "session_not_found" or "version_not_found" =>
                (StatusCodes.Status404NotFound, application.Code),
            PlayException application => (StatusCodes.Status422UnprocessableEntity, application.Code),
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