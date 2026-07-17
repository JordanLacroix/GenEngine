using System.Collections.Concurrent;

using GenEngine.Observability;

using Microsoft.Extensions.Logging;

namespace GenEngine.Services.Tests;

public sealed class AuditLogTests
{
    [Fact]
    public void RecordEmitsActionAndOutcomeWithAuditScope()
    {
        (AuditLog audit, CapturingLoggerProvider capture) = CreateAudit();

        audit.Record(new AuditEvent
        {
            Action = "scenario_published",
            Outcome = AuditOutcome.Success,
            ActorId = "actor-1",
            ResourceType = "scenario",
            ResourceId = "scenario-1",
        });

        CapturedLog entry = Assert.Single(capture.Entries);
        Assert.Equal(LogLevel.Information, entry.Level);
        Assert.Equal("Audit", entry.EventName);
        Assert.Equal(true, entry.Scope["audit"]);
        Assert.Equal("scenario_published", entry.Scope["audit.action"]);
        Assert.Equal("Success", entry.Scope["audit.outcome"]);
        Assert.Equal("actor-1", entry.Scope["audit.actor_id"]);
        Assert.Equal("scenario-1", entry.Scope["audit.resource_id"]);
    }

    [Fact]
    public void FailureOutcomeIsLoggedAsWarning()
    {
        (AuditLog audit, CapturingLoggerProvider capture) = CreateAudit();

        audit.Record(new AuditEvent { Action = "login_failed", Outcome = AuditOutcome.Failure });

        CapturedLog entry = Assert.Single(capture.Entries);
        Assert.Equal(LogLevel.Warning, entry.Level);
    }

    [Theory]
    [InlineData("password")]
    [InlineData("Password")]
    [InlineData("user_email")]
    [InlineData("access_token")]
    [InlineData("secret_key")]
    [InlineData("password_hash")]
    public void SensitivePropertiesAreDroppedAndNeverLogged(string sensitiveKey)
    {
        (AuditLog audit, CapturingLoggerProvider capture) = CreateAudit();
        const string secretValue = "must-never-appear-42";

        audit.Record(new AuditEvent
        {
            Action = "user_registered",
            Outcome = AuditOutcome.Success,
            Properties = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [sensitiveKey] = secretValue,
                ["safe_key"] = "kept",
            },
        });

        CapturedLog entry = Assert.Single(capture.Entries);
        Assert.DoesNotContain(entry.Scope, pair => Equals(pair.Value, secretValue));
        Assert.DoesNotContain(entry.Scope.Keys, key => key.Contains(sensitiveKey, StringComparison.OrdinalIgnoreCase));
        Assert.Equal("kept", entry.Scope["audit.property.safe_key"]);
        Assert.Equal(1, entry.Scope["audit.dropped_properties"]);
    }

    private static (AuditLog, CapturingLoggerProvider) CreateAudit()
    {
        var capture = new CapturingLoggerProvider();
        using var factory = LoggerFactory.Create(builder =>
        {
            builder.SetMinimumLevel(LogLevel.Trace);
            builder.AddProvider(capture);
        });
        return (new AuditLog(factory.CreateLogger<AuditLog>()), capture);
    }
}

internal sealed record CapturedLog(
    LogLevel Level,
    string? EventName,
    IReadOnlyDictionary<string, object?> Scope);

internal sealed class CapturingLoggerProvider : ILoggerProvider
{
    public ConcurrentQueue<CapturedLog> Entries { get; } = new();

    public ILogger CreateLogger(string categoryName) => new CapturingLogger(this);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(CapturingLoggerProvider provider) : ILogger
    {
        private readonly AsyncLocal<Dictionary<string, object?>> current = new();

        public IDisposable BeginScope<TState>(TState state)
            where TState : notnull
        {
            var merged = new Dictionary<string, object?>(StringComparer.Ordinal);
            if (current.Value is { } existing)
            {
                foreach ((string key, object? value) in existing)
                {
                    merged[key] = value;
                }
            }

            if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
            {
                foreach ((string key, object? value) in pairs)
                {
                    merged[key] = value;
                }
            }

            current.Value = merged;
            return new ScopeToken(this, existing: current.Value);
        }

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            var scope = current.Value is { } value
                ? new Dictionary<string, object?>(value, StringComparer.Ordinal)
                : new Dictionary<string, object?>(StringComparer.Ordinal);
            provider.Entries.Enqueue(new CapturedLog(logLevel, eventId.Name, scope));
        }

        private sealed class ScopeToken(CapturingLogger logger, Dictionary<string, object?>? existing) : IDisposable
        {
            public void Dispose() => logger.current.Value = existing!;
        }
    }
}