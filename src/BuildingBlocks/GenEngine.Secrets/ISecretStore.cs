namespace GenEngine.Secrets;

/// <summary>
/// Resolves references of a single scheme. One implementation per backend: environment
/// variables today, a vault client later. Implementations must never throw for a missing
/// secret and must never include the identifier or any secret fragment in an exception.
/// </summary>
public interface ISecretResolver
{
    /// <summary>The scheme this resolver claims, lowercase, without the colon.</summary>
    string Scheme { get; }

    /// <summary>Resolves <paramref name="reference"/>, returning a failure value when absent.</summary>
    ValueTask<SecretResolution> ResolveAsync(SecretReference reference, CancellationToken cancellationToken);
}

/// <summary>
/// The front door used by services that call external providers. It takes the raw reference
/// stored in the configuration document and never exposes anything but a
/// <see cref="SecretResolution"/>.
/// </summary>
public interface ISecretStore
{
    /// <summary>
    /// Resolves a raw reference. A null, blank, malformed or unknown reference yields a failed
    /// resolution rather than an exception, so the caller can treat the provider as unconfigured.
    /// </summary>
    ValueTask<SecretResolution> ResolveAsync(string? rawReference, CancellationToken cancellationToken);
}