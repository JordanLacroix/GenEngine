namespace GenEngine.Secrets;

/// <summary>
/// A resolved secret. Every implicit rendering path — <see cref="ToString"/>, string
/// interpolation, structured log formatting, <c>System.Text.Json</c> serialization of the
/// public surface — yields <see cref="Redacted"/>. The clear value is only reachable through
/// the deliberate <see cref="Reveal"/> call, which is the single place to audit.
/// </summary>
public readonly struct SecretValue : IEquatable<SecretValue>, IFormattable
{
    /// <summary>The placeholder substituted for the secret in any rendered output.</summary>
    public const string Redacted = "***";

    private readonly string? value;

    /// <summary>Wraps a clear secret. Callers must not retain the original string.</summary>
    public SecretValue(string value) =>
        this.value = value ?? throw new ArgumentNullException(nameof(value));

    /// <summary>Whether this instance actually holds a secret.</summary>
    public bool HasValue => value is not null;

    /// <summary>
    /// Returns the clear secret. Call this only at the boundary that hands the credential to
    /// the external provider, never before logging, serializing or building a message.
    /// </summary>
    public string Reveal() => value
        ?? throw new InvalidOperationException("No secret value was resolved.");

    /// <inheritdoc />
    public override string ToString() => Redacted;

    /// <inheritdoc />
    public string ToString(string? format, IFormatProvider? formatProvider) => Redacted;

    /// <inheritdoc />
    public bool Equals(SecretValue other) => string.Equals(value, other.value, StringComparison.Ordinal);

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is SecretValue other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => value is null ? 0 : StringComparer.Ordinal.GetHashCode(value);

    /// <summary>Value equality over the clear secrets.</summary>
    public static bool operator ==(SecretValue left, SecretValue right) => left.Equals(right);

    /// <summary>Value inequality over the clear secrets.</summary>
    public static bool operator !=(SecretValue left, SecretValue right) => !left.Equals(right);
}