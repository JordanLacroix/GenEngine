using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;

namespace GenEngine.Services.Tests;

public sealed class ConfigurationServiceTests
{
    [Fact]
    public async Task PublishedViewRedactsSecretReferenceAndKeepsConfiguredExperience()
    {
        var repository = new ConfigurationRepositoryStub();
        var service = new ConfigurationService(repository, TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("company-demo") with
        {
            OrganizationType = OrganizationType.Company,
            Authentication = new AuthenticationDefinition(AuthenticationMode.Cumulative, true, true, "tenant-id", "client-id"),
        };

        ExperienceConfigurationView created = await service.UpsertAsync("company-demo", null, document, CancellationToken.None);
        await service.PublishAsync("company-demo", created.Revision, CancellationToken.None);
        PublishedExperienceView published = await service.GetPublishedAsync("company-demo", CancellationToken.None);

        Assert.Equal(OrganizationType.Company, published.Document.OrganizationType);
        Assert.Equal(AuthenticationMode.Cumulative, published.Document.Authentication.Mode);
        Assert.All(published.Document.AiProviders, static provider => Assert.Null(provider.SecretReference));
        Assert.Contains(published.Document.Economy.RewardRules, static rule => rule.Trigger == "ScenarioCompleted");
    }

    [Fact]
    public async Task UpdateRejectsAConfigurationWithoutAuthenticationProvider()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("default") with
        {
            Authentication = new AuthenticationDefinition(AuthenticationMode.LocalOnly, false, false, null, null),
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, document, CancellationToken.None));

        Assert.Equal("invalid_authentication", exception.Code);
    }

    private sealed class ConfigurationRepositoryStub : IConfigurationRepository
    {
        private ExperienceConfiguration? configuration;
        public Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task AddAsync(ExperienceConfiguration value, CancellationToken cancellationToken) { configuration = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}