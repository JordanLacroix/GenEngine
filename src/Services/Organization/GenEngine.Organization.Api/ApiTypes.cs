using GenEngine.Organization.Domain;

using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GenEngine.Organization.Api;

public sealed record UpsertFrontRequest(string Name, string Type, bool IsActive, int? ExpectedRevision);
public sealed record UpsertPeriodRequest(string Name, string Code, DateTimeOffset StartsAt, DateTimeOffset EndsAt, bool IsActive, int? ExpectedRevision);
public sealed record UpsertUnitRequest(Guid? ParentId, string Name, string Type, string Code, bool IsActive, int? ExpectedRevision);
public sealed record UpsertMembershipRequest(Guid UnitId, Guid UserId, Guid? PeriodId, MembershipKind Kind, DateTimeOffset StartsAt, DateTimeOffset? EndsAt, bool IsActive, int? ExpectedRevision);
public sealed record ImportMembershipsRequest(bool DryRun, IReadOnlyList<Application.MembershipImportRow> Rows);
public sealed record UpsertAssignmentRequest(Guid UnitId, AssignedContentType ContentType, Guid ContentId, string Name, bool Required, DateTimeOffset? AvailableFrom, DateTimeOffset? DueAt, bool IsActive, int? ExpectedRevision);

public sealed class ApiExceptionHandler(IProblemDetailsService problemDetailsService) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        (int status, string code) = exception switch
        {
            OrganizationDomainException domain when domain.Code == "revision_conflict" => (StatusCodes.Status409Conflict, domain.Code),
            OrganizationDomainException domain => (StatusCodes.Status400BadRequest, domain.Code),
            Application.OrganizationException application when application.Code.EndsWith("_not_found", StringComparison.Ordinal) => (StatusCodes.Status404NotFound, application.Code),
            Application.OrganizationException application when application.Code is "revision_conflict" or "organization_conflict" => (StatusCodes.Status409Conflict, application.Code),
            Application.OrganizationException application => (StatusCodes.Status422UnprocessableEntity, application.Code),
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