namespace GenEngine.Secrets;

/// <summary>
/// Dispatches a raw reference to the resolver registered for its scheme.
/// </summary>
/// <remarks>
/// Extending the grammar means registering one more <see cref="ISecretResolver"/>; no parsing
/// rule changes. The <c>vault:</c> scheme is reserved for a future coffre-fort client
/// (Azure Key Vault or equivalent): until such a resolver is registered, a <c>vault:</c>
/// reference resolves to <see cref="SecretResolutionFailure.UnsupportedScheme"/> and the
/// provider is simply seen as not configured.
/// </remarks>
public sealed class SecretStore : ISecretStore
{
    /// <summary>Scheme reserved for a vault backed resolver that this repository does not ship.</summary>
    public const string ReservedVaultScheme = "vault";

    private readonly Dictionary<string, ISecretResolver> resolvers;

    /// <summary>Builds a store over <paramref name="resolvers"/>; duplicate schemes are rejected.</summary>
    public SecretStore(IEnumerable<ISecretResolver> resolvers)
    {
        ArgumentNullException.ThrowIfNull(resolvers);

        var map = new Dictionary<string, ISecretResolver>(StringComparer.Ordinal);
        foreach (ISecretResolver resolver in resolvers)
        {
            if (!map.TryAdd(resolver.Scheme, resolver))
            {
                throw new InvalidOperationException(
                    $"More than one secret resolver claims the '{resolver.Scheme}' scheme.");
            }
        }

        this.resolvers = map;
    }

    /// <summary>The local default: environment variables only.</summary>
    public static SecretStore CreateLocal() => new([new EnvironmentSecretResolver()]);

    /// <inheritdoc />
    public async ValueTask<SecretResolution> ResolveAsync(string? rawReference, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(rawReference))
        {
            return SecretResolution.Failed(SecretResolutionFailure.NotConfigured);
        }

        if (!SecretReference.TryParse(rawReference, out SecretReference reference))
        {
            return SecretResolution.Failed(SecretResolutionFailure.MalformedReference);
        }

        if (!resolvers.TryGetValue(reference.Scheme, out ISecretResolver? resolver))
        {
            return SecretResolution.Failed(SecretResolutionFailure.UnsupportedScheme);
        }

        try
        {
            return await resolver.ResolveAsync(reference, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception)
        {
            // A backend failure must never surface the reference or a fragment of the secret
            // through an exception message, so the cause is collapsed to a safe value.
            return SecretResolution.Failed(SecretResolutionFailure.NotFound);
        }
    }
}