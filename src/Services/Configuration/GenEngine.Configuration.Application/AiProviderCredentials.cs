using GenEngine.Secrets;

namespace GenEngine.Configuration.Application;

/// <summary>
/// Whether a configured AI provider can actually authenticate. <see cref="Reason"/> is a stable,
/// reference-free code: it is safe in an API payload, an audit entry and a log line.
/// </summary>
public sealed record AiProviderAvailability(
    Guid ProviderId,
    string Name,
    AiProviderType Type,
    bool IsUsable,
    string Reason);

/// <summary>
/// Turns the opaque <see cref="AiProviderDefinition.SecretReference"/> into a usable credential.
/// A provider whose secret cannot be resolved is reported as not usable; callers degrade to the
/// offline provider instead of failing.
/// </summary>
public sealed class AiProviderCredentialResolver(ISecretStore secretStore)
{
    /// <summary>Reported when the provider is switched off in the configuration document.</summary>
    public const string DisabledReason = "provider_disabled";

    /// <summary>Reported when the provider needs no credential, such as the offline provider.</summary>
    public const string NoCredentialRequiredReason = "no_credential_required";

    /// <summary>Reported when the credential resolved successfully.</summary>
    public const string ReadyReason = "ready";

    private readonly ISecretStore secretStore = secretStore
        ?? throw new ArgumentNullException(nameof(secretStore));

    /// <summary>
    /// Resolves the credential for <paramref name="provider"/>. Returns <see langword="null"/> when
    /// the provider is disabled, needs no credential, or its reference cannot be resolved.
    /// </summary>
    public async Task<SecretValue?> ResolveCredentialAsync(
        AiProviderDefinition provider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!provider.Enabled || !RequiresCredential(provider))
        {
            return null;
        }

        SecretResolution resolution = await secretStore
            .ResolveAsync(provider.SecretReference, cancellationToken)
            .ConfigureAwait(false);

        return resolution.Succeeded ? resolution.Value : null;
    }

    /// <summary>Describes a single provider without revealing its reference or its secret.</summary>
    public async Task<AiProviderAvailability> DescribeAsync(
        AiProviderDefinition provider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(provider);

        if (!provider.Enabled)
        {
            return Describe(provider, false, DisabledReason);
        }

        if (!RequiresCredential(provider))
        {
            return Describe(provider, true, NoCredentialRequiredReason);
        }

        SecretResolution resolution = await secretStore
            .ResolveAsync(provider.SecretReference, cancellationToken)
            .ConfigureAwait(false);

        return Describe(provider, resolution.Succeeded, resolution.ToSafeReason());
    }

    /// <summary>Describes every provider of <paramref name="document"/>.</summary>
    public async Task<IReadOnlyList<AiProviderAvailability>> DescribeAllAsync(
        ExperienceDocument document,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);

        var availabilities = new List<AiProviderAvailability>(document.AiProviders.Count);
        foreach (AiProviderDefinition provider in document.AiProviders)
        {
            availabilities.Add(await DescribeAsync(provider, cancellationToken).ConfigureAwait(false));
        }

        return availabilities;
    }

    private static bool RequiresCredential(AiProviderDefinition provider) =>
        provider.Type != AiProviderType.Offline
        && !string.IsNullOrWhiteSpace(provider.SecretReference);

    private static AiProviderAvailability Describe(AiProviderDefinition provider, bool isUsable, string reason) =>
        new(provider.Id, provider.Name, provider.Type, isUsable, reason);
}