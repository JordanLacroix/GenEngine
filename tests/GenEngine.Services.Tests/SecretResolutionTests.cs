using System.Globalization;
using System.Text.Json;

using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;
using GenEngine.Secrets;

namespace GenEngine.Services.Tests;

public sealed class SecretResolutionTests
{
    private const string ClearSecret = "s3cr3t-value-that-must-never-leak";
    private const string Reference = "env:GENENGINE_AI_AZURE_FOUNDRY_KEY";

    private static SecretStore StoreWith(params (string Name, string Value)[] variables)
    {
        var environment = variables.ToDictionary(
            static variable => variable.Name,
            static variable => variable.Value,
            StringComparer.Ordinal);

        return new SecretStore([
            new EnvironmentSecretResolver(name => environment.GetValueOrDefault(name)),
        ]);
    }

    [Fact]
    public async Task LocalResolverReturnsTheSecretHeldInTheEnvironment()
    {
        SecretStore store = StoreWith(("GENENGINE_AI_AZURE_FOUNDRY_KEY", ClearSecret));

        SecretResolution resolution = await store.ResolveAsync(Reference, CancellationToken.None);

        Assert.True(resolution.Succeeded);
        Assert.Equal(SecretResolutionFailure.None, resolution.Failure);
        Assert.Equal(ClearSecret, resolution.Value.Reveal());
    }

    [Fact]
    public async Task AMissingSecretMakesTheProviderUnconfiguredWithoutLeakingAnything()
    {
        SecretStore store = StoreWith();
        var resolver = new AiProviderCredentialResolver(store);
        AiProviderDefinition provider = AzureProvider(Reference);

        AiProviderAvailability availability = await resolver.DescribeAsync(provider, CancellationToken.None);
        SecretValue? credential = await resolver.ResolveCredentialAsync(provider, CancellationToken.None);

        Assert.False(availability.IsUsable);
        Assert.Equal("secret_not_found", availability.Reason);
        Assert.Null(credential);

        // Neither the reference nor any fragment of it travels with the failure.
        Assert.DoesNotContain("GENENGINE", availability.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("env:", availability.Reason, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("FOUNDRY", availability.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task AResolutionFailureIsAValueAndNeverAnException()
    {
        SecretStore store = new([new ThrowingResolver()]);

        SecretResolution resolution = await store.ResolveAsync("boom:anything", CancellationToken.None);

        Assert.False(resolution.Succeeded);
        Assert.Equal(SecretResolutionFailure.NotFound, resolution.Failure);
        Assert.DoesNotContain(ClearSecret, resolution.ToSafeReason(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task ABlankReferenceIsReportedAsNotConfigured(string? raw)
    {
        SecretResolution resolution = await StoreWith().ResolveAsync(raw, CancellationToken.None);

        Assert.Equal(SecretResolutionFailure.NotConfigured, resolution.Failure);
    }

    [Theory]
    [InlineData("no-scheme")]                 // missing separator
    [InlineData(":GENENGINE_KEY")]            // empty scheme
    [InlineData("env:")]                      // empty identifier
    [InlineData("ENV:GENENGINE_KEY")]         // uppercase scheme
    [InlineData("1env:GENENGINE_KEY")]        // scheme starts with a digit
    [InlineData("en v:GENENGINE_KEY")]        // whitespace in the scheme
    [InlineData("env:GENENGINE KEY")]         // whitespace in the identifier
    [InlineData("env:GENENGINE\nKEY")]        // control character
    public void AnInvalidGrammarIsRejectedExplicitly(string raw)
    {
        Assert.False(SecretReference.TryParse(raw, out _));
        Assert.False(SecretReferenceGrammar.IsWellFormed(raw));
    }

    [Theory]
    [InlineData("env:GENENGINE_AI_AZURE_FOUNDRY_KEY", "env", "GENENGINE_AI_AZURE_FOUNDRY_KEY")]
    [InlineData("vault:genengine/ai/foundry", "vault", "genengine/ai/foundry")]
    [InlineData("aws-secrets:prod/ai-key", "aws-secrets", "prod/ai-key")]
    public void AWellFormedReferenceIsParsedIntoSchemeAndIdentifier(string raw, string scheme, string identifier)
    {
        Assert.True(SecretReference.TryParse(raw, out SecretReference reference));
        Assert.Equal(scheme, reference.Scheme);
        Assert.Equal(identifier, reference.Identifier);
        Assert.Equal(raw, reference.ToString());
    }

    [Fact]
    public async Task AnInvalidGrammarIsRejectedWhenTheConfigurationIsSaved()
    {
        var service = new ConfigurationService(new RepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");
        ExperienceDocument document = baseline with
        {
            AiProviders = baseline.AiProviders
                .Select(static provider => provider with { SecretReference = "not a reference" })
                .ToArray(),
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, document, CancellationToken.None));

        Assert.Equal("invalid_secret_reference", exception.Code);
        Assert.DoesNotContain("not a reference", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task AVaultReferenceDegradesUntilAVaultResolverIsRegistered()
    {
        SecretStore store = StoreWith();

        SecretResolution resolution = await store.ResolveAsync("vault:genengine/ai/foundry", CancellationToken.None);

        Assert.False(resolution.Succeeded);
        Assert.Equal(SecretResolutionFailure.UnsupportedScheme, resolution.Failure);
        Assert.Equal("secret_scheme_unsupported", resolution.ToSafeReason());
    }

    [Fact]
    public async Task TheOfflineProviderStaysUsableWithoutAnySecret()
    {
        var resolver = new AiProviderCredentialResolver(StoreWith());
        var offline = new AiProviderDefinition(
            Guid.NewGuid(), "Hors ligne", AiProviderType.Offline, true,
            string.Empty, "deterministic", "None", null, ["chat"]);

        AiProviderAvailability availability = await resolver.DescribeAsync(offline, CancellationToken.None);

        Assert.True(availability.IsUsable);
        Assert.Equal(AiProviderCredentialResolver.NoCredentialRequiredReason, availability.Reason);
    }

    [Fact]
    public async Task ADisabledProviderIsNeverAskedForItsCredential()
    {
        var resolver = new AiProviderCredentialResolver(
            StoreWith(("GENENGINE_AI_AZURE_FOUNDRY_KEY", ClearSecret)));
        AiProviderDefinition disabled = AzureProvider(Reference) with { Enabled = false };

        AiProviderAvailability availability = await resolver.DescribeAsync(disabled, CancellationToken.None);

        Assert.False(availability.IsUsable);
        Assert.Equal(AiProviderCredentialResolver.DisabledReason, availability.Reason);
        Assert.Null(await resolver.ResolveCredentialAsync(disabled, CancellationToken.None));
    }

    // The test that matters most: with the secret actually resolvable, prove that no
    // rendering path — client projection, log line, availability payload — can carry it.
    [Fact]
    public async Task NoSecretCanReachAClientProjectionAnAvailabilityPayloadOrALog()
    {
        var repository = new RepositoryStub();
        var service = new ConfigurationService(repository, TimeProvider.System);
        SecretStore store = StoreWith(("GENENGINE_AI_AZURE_FOUNDRY_KEY", ClearSecret));
        var credentials = new AiProviderCredentialResolver(store);

        ExperienceConfigurationView created = await service.UpsertAsync(
            "default", null, ConfigurationService.CreateDefault("default"), CancellationToken.None);
        await service.PublishAsync("default", created.Revision, CancellationToken.None);
        PublishedExperienceView published = await service.GetPublishedAsync("default", CancellationToken.None);

        // The secret is genuinely resolvable in this test, so an absence below proves redaction,
        // not a missing value.
        SecretResolution resolution = await store.ResolveAsync(Reference, CancellationToken.None);
        Assert.True(resolution.Succeeded);

        // 1. The client projection carries neither the secret nor the reference.
        string clientJson = JsonSerializer.Serialize(published);
        Assert.DoesNotContain(ClearSecret, clientJson, StringComparison.Ordinal);
        Assert.DoesNotContain(Reference, clientJson, StringComparison.Ordinal);
        Assert.DoesNotContain("GENENGINE_AI_AZURE_FOUNDRY_KEY", clientJson, StringComparison.Ordinal);
        Assert.All(published.Document.AiProviders, static provider => Assert.Null(provider.SecretReference));

        // 2. The availability payload exposed to operators carries neither.
        IReadOnlyList<AiProviderAvailability> availabilities =
            await credentials.DescribeAllAsync(published.Document, CancellationToken.None);
        string availabilityJson = JsonSerializer.Serialize(availabilities);
        Assert.DoesNotContain(ClearSecret, availabilityJson, StringComparison.Ordinal);
        Assert.DoesNotContain("GENENGINE_AI_AZURE_FOUNDRY_KEY", availabilityJson, StringComparison.Ordinal);

        // 3. Every implicit rendering path of the resolved secret yields the placeholder.
        SecretValue secret = resolution.Value;
        var log = new List<string>
        {
            secret.ToString(),
            $"credential={secret}",
            string.Format(CultureInfo.InvariantCulture, "credential={0}", secret),
            string.Concat("credential=", secret),
            JsonSerializer.Serialize(new { Credential = secret.ToString(), resolution.Failure }),
            resolution.ToString(),
            resolution.ToSafeReason(),
        };

        Assert.All(log, static line => Assert.DoesNotContain(ClearSecret, line, StringComparison.Ordinal));
        Assert.Contains(SecretValue.Redacted, $"{secret}", StringComparison.Ordinal);

        // 4. Only the deliberate Reveal call yields the clear value.
        Assert.Equal(ClearSecret, secret.Reveal());
    }

    private static AiProviderDefinition AzureProvider(string? reference) =>
        new(Guid.NewGuid(), "Azure AI Foundry", AiProviderType.AzureAiFoundry, true,
            "https://resource.openai.azure.com/openai/v1/", "gpt-4.1-mini", "EntraId", reference,
            ["chat"]);

    private sealed class ThrowingResolver : ISecretResolver
    {
        public string Scheme => "boom";

        public ValueTask<SecretResolution> ResolveAsync(SecretReference reference, CancellationToken cancellationToken) =>
            throw new InvalidOperationException($"backend failed for {reference} holding {ClearSecret}");
    }

    private sealed class RepositoryStub : IConfigurationRepository
    {
        private ExperienceConfiguration? configuration;
        public Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task AddAsync(ExperienceConfiguration value, CancellationToken cancellationToken) { configuration = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}