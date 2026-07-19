namespace GenEngine.Secrets;

/// <summary>
/// Why a reference did not yield a secret. The enumeration is deliberately coarse and
/// closed: it is the only failure detail that leaves the resolver, so no reference text,
/// backend path or secret fragment can travel with it.
/// </summary>
public enum SecretResolutionFailure
{
    /// <summary>No failure; the resolution succeeded.</summary>
    None = 0,

    /// <summary>No reference was configured at all.</summary>
    NotConfigured,

    /// <summary>The reference does not match the <c>scheme:identifier</c> grammar.</summary>
    MalformedReference,

    /// <summary>The grammar is valid but no resolver is registered for that scheme.</summary>
    UnsupportedScheme,

    /// <summary>The scheme is supported but the backend holds no value under that identifier.</summary>
    NotFound,
}

/// <summary>
/// The outcome of resolving a reference. Failures are values, not exceptions: a provider whose
/// secret cannot be resolved is treated as not configured and the caller degrades.
/// </summary>
public readonly struct SecretResolution : IEquatable<SecretResolution>
{
    private SecretResolution(bool succeeded, SecretValue value, SecretResolutionFailure failure)
    {
        Succeeded = succeeded;
        Value = value;
        Failure = failure;
    }

    /// <summary>Whether a secret was found.</summary>
    public bool Succeeded { get; }

    /// <summary>The resolved secret; meaningful only when <see cref="Succeeded"/> is true.</summary>
    public SecretValue Value { get; }

    /// <summary>The failure cause; <see cref="SecretResolutionFailure.None"/> on success.</summary>
    public SecretResolutionFailure Failure { get; }

    /// <summary>Builds a successful resolution.</summary>
    public static SecretResolution Success(SecretValue value) => new(true, value, SecretResolutionFailure.None);

    /// <summary>Builds a failed resolution carrying only a coarse cause.</summary>
    public static SecretResolution Failed(SecretResolutionFailure failure) =>
        failure == SecretResolutionFailure.None
            ? throw new ArgumentOutOfRangeException(nameof(failure), "A failed resolution requires a cause.")
            : new SecretResolution(false, default, failure);

    /// <summary>
    /// A short, reference-free reason suitable for logs, audit entries and API payloads.
    /// </summary>
    public string ToSafeReason() => Failure switch
    {
        SecretResolutionFailure.None => "resolved",
        SecretResolutionFailure.NotConfigured => "secret_not_configured",
        SecretResolutionFailure.MalformedReference => "secret_reference_malformed",
        SecretResolutionFailure.UnsupportedScheme => "secret_scheme_unsupported",
        SecretResolutionFailure.NotFound => "secret_not_found",
        _ => "secret_unavailable",
    };

    /// <summary>Renders the safe reason; never the secret.</summary>
    public override string ToString() => ToSafeReason();

    /// <inheritdoc />
    public bool Equals(SecretResolution other) =>
        Succeeded == other.Succeeded && Failure == other.Failure && Value.Equals(other.Value);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SecretResolution other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => HashCode.Combine(Succeeded, Failure, Value);

    /// <summary>Value equality.</summary>
    public static bool operator ==(SecretResolution left, SecretResolution right) => left.Equals(right);

    /// <summary>Value inequality.</summary>
    public static bool operator !=(SecretResolution left, SecretResolution right) => !left.Equals(right);
}