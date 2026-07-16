using System.Collections.Generic;
using System.Linq;

using Microsoft.Extensions.Logging;

namespace GenEngine.Observability;

/// <summary>Outcome of an audited operation.</summary>
public enum AuditOutcome
{
    Success,
    Failure,
    Denied,
}

/// <summary>
/// A single business audit record. This is a technical envelope only: the
/// business meaning (the <see cref="Action"/> name and which identifiers are
/// relevant) is decided by the calling service, never here.
/// </summary>
public sealed record AuditEvent
{
    /// <summary>Stable, machine-readable action name, e.g. "scenario_published".</summary>
    public required string Action { get; init; }

    /// <summary>Whether the operation succeeded, failed or was denied.</summary>
    public required AuditOutcome Outcome { get; init; }

    /// <summary>Identifier of the actor (never a name, email or token).</summary>
    public string? ActorId { get; init; }

    /// <summary>Type of the affected resource, e.g. "scenario" or "session".</summary>
    public string? ResourceType { get; init; }

    /// <summary>Identifier of the affected resource.</summary>
    public string? ResourceId { get; init; }

    /// <summary>Extra non-sensitive properties. Sensitive keys are dropped on write.</summary>
    public IReadOnlyDictionary<string, string>? Properties { get; init; }
}

/// <summary>Emits business audit records as structured logs.</summary>
public interface IAuditLog
{
    void Record(AuditEvent auditEvent);
}

/// <summary>
/// Writes audit records through <see cref="ILogger"/> so they flow to the same
/// structured-logging pipeline (console + OTLP) as the rest of the platform.
/// Every record carries an "audit" scope flag so it can be isolated in Loki.
/// A deny-list drops obviously sensitive extra properties as defense in depth;
/// callers are still expected never to pass secrets or personal data.
/// </summary>
public sealed partial class AuditLog(ILogger<AuditLog> logger) : IAuditLog
{

    // Substrings that must never appear as property keys in an audit record.
    private static readonly string[] SensitiveKeyFragments =
    [
        "password", "passwd", "pwd", "secret", "token", "authorization",
        "credential", "apikey", "api_key", "hash", "email", "mail",
    ];

    public void Record(AuditEvent auditEvent)
    {
        ArgumentNullException.ThrowIfNull(auditEvent);
        ArgumentException.ThrowIfNullOrWhiteSpace(auditEvent.Action);

        (IReadOnlyDictionary<string, string> safeProperties, int droppedCount) =
            Sanitize(auditEvent.Properties);

        Dictionary<string, object?> scope = new(StringComparer.Ordinal)
        {
            ["audit"] = true,
            ["audit.action"] = auditEvent.Action,
            ["audit.outcome"] = auditEvent.Outcome.ToString(),
            ["audit.actor_id"] = auditEvent.ActorId,
            ["audit.resource_type"] = auditEvent.ResourceType,
            ["audit.resource_id"] = auditEvent.ResourceId,
        };

        foreach ((string key, string value) in safeProperties)
        {
            scope[$"audit.property.{key}"] = value;
        }

        if (droppedCount > 0)
        {
            scope["audit.dropped_properties"] = droppedCount;
        }

        LogLevel level = auditEvent.Outcome == AuditOutcome.Success
            ? LogLevel.Information
            : LogLevel.Warning;

        using (logger.BeginScope(scope))
        {
            LogAudit(logger, level, auditEvent.Action, auditEvent.Outcome);
        }
    }

    [LoggerMessage(EventId = 1000, EventName = "Audit", Message = "audit {AuditAction} {AuditOutcome}")]
    private static partial void LogAudit(
        ILogger logger,
        LogLevel level,
        string auditAction,
        AuditOutcome auditOutcome);

    private static (IReadOnlyDictionary<string, string>, int) Sanitize(
        IReadOnlyDictionary<string, string>? properties)
    {
        if (properties is null || properties.Count == 0)
        {
            return (EmptyProperties, 0);
        }

        Dictionary<string, string> safe = new(StringComparer.Ordinal);
        int dropped = 0;

        foreach ((string key, string value) in properties)
        {
            if (IsSensitiveKey(key))
            {
                dropped++;
                continue;
            }

            safe[key] = value;
        }

        return (safe, dropped);
    }

    private static bool IsSensitiveKey(string key) =>
        SensitiveKeyFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static readonly IReadOnlyDictionary<string, string> EmptyProperties =
        new Dictionary<string, string>(StringComparer.Ordinal);
}