using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Domain;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GenEngine.PlayerExperience.Api;

public sealed record ConfigureFamiliarRequest(int ExpectedRevision, FamiliarSelection Selection);
public sealed record PurchaseRequest(Guid OfferId, string IdempotencyKey);
public sealed record OnboardingCommandRequest(string IdempotencyKey);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string code) = exception switch
        {
            PlayerExperienceDomainException domain when domain.Code == "revision_conflict" => (StatusCodes.Status409Conflict, domain.Code),
            PlayerExperienceDomainException domain => (StatusCodes.Status400BadRequest, domain.Code),
            PlayerExperienceException application when application.Code == "revision_conflict" => (StatusCodes.Status409Conflict, application.Code),
            PlayerExperienceException application => (StatusCodes.Status400BadRequest, application.Code),
            _ => (StatusCodes.Status500InternalServerError, "internal_error"),
        };
        httpContext.Response.StatusCode = status;
        return await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = httpContext,
            ProblemDetails = new ProblemDetails { Status = status, Title = code, Detail = status == 500 ? null : exception.Message },
        }).ConfigureAwait(false);
    }
}
