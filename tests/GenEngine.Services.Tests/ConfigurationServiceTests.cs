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
        Assert.NotNull(published.Document.Organization);
        Assert.Equal(AuthenticationMode.Cumulative, published.Document.Authentication.Mode);
        Assert.Equal("Studio", published.Document.Language?.Labels["nav.studio"]);
        Assert.All(published.Document.AiProviders, static provider => Assert.Null(provider.SecretReference));
        Assert.Contains(published.Document.Economy.RewardRules, static rule => rule.Trigger == "ScenarioCompleted");
    }

    [Fact]
    public async Task CustomGameWordingIsPublishedAndDefaultsRemainAvailable()
    {
        var repository = new ConfigurationRepositoryStub();
        var service = new ConfigurationService(repository, TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("academy") with
        {
            Language = new GameLanguageDefinition(new Dictionary<string, string>
            {
                ["nav.studio"] = "Forge des récits",
                ["entity.scenario.plural"] = "Missions",
            }),
        };

        ExperienceConfigurationView created = await service.UpsertAsync("academy", null, document, CancellationToken.None);
        await service.PublishAsync("academy", created.Revision, CancellationToken.None);
        PublishedExperienceView published = await service.GetPublishedAsync("academy", CancellationToken.None);

        Assert.Equal("Forge des récits", published.Document.Language?.Labels["nav.studio"]);
        Assert.Equal("Missions", published.Document.Language?.Labels["entity.scenario.plural"]);
        Assert.Equal("Accueil", published.Document.Language?.Labels["nav.home"]);
    }

    [Fact]
    public async Task OrganizationHierarchyRejectsCycles()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        Guid first = Guid.NewGuid();
        Guid second = Guid.NewGuid();
        ExperienceDocument document = ConfigurationService.CreateDefault("school-demo") with
        {
            OrganizationType = OrganizationType.School,
            Organization = new OrganizationDefinition("Collège", "Structure pédagogique",
            [
                new OrganizationUnitDefinition(first, second, "Class", "6e A", "6A", "", 1, true),
                new OrganizationUnitDefinition(second, first, "Group", "Groupe 1", "G1", "", 2, true),
            ]),
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("school-demo", null, document, CancellationToken.None));

        Assert.Equal("organization_cycle", exception.Code);
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

    [Fact]
    public async Task JourneyRejectsUnknownCategory()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");
        ExperienceDocument document = baseline with
        {
            Journeys =
            [
                new JourneyDefinition(Guid.NewGuid(), "Parcours invalide", "", "ember", null, 1, true, [Guid.NewGuid()], [], []),
            ],
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, document, CancellationToken.None));

        Assert.Equal("invalid_journey", exception.Code);
    }

    [Fact]
    public async Task FamiliarRejectsInsecureAssetUrl()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");
        ExperienceDocument document = baseline with
        {
            Familiars = baseline.Familiars.Select(familiar => familiar with { PortraitUrl = "http://insecure.example/avatar.png" }).ToArray(),
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, document, CancellationToken.None));

        Assert.Equal("invalid_familiar", exception.Code);
    }

    [Fact]
    public async Task AppLocationAndGameOverMediaArePublishedPerInstance()
    {
        var repository = new ConfigurationRepositoryStub();
        var service = new ConfigurationService(repository, TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("studio") with
        {
            Media = new MediaDefinition(
                true,
                false,
                [
                    new AppLocationMediaDefinition(
                        "map",
                        "https://assets.example.org/ambience-map-v1.ogg",
                        "https://assets.example.org/music-map-v1.ogg",
                        Bpm: 64),
                    new AppLocationMediaDefinition("journal"),
                ],
                new GameOverMediaDefinition(
                    "https://assets.example.org/music-game-over-v1.ogg",
                    "https://assets.example.org/scene-game-over-v1.avif",
                    "Une brume éteinte recouvre le chemin parcouru.",
                    "gameOver.title")),
        };

        ExperienceConfigurationView created = await service.UpsertAsync("studio", null, document, CancellationToken.None);
        await service.PublishAsync("studio", created.Revision, CancellationToken.None);
        PublishedExperienceView published = await service.GetPublishedAsync("studio", CancellationToken.None);

        AppLocationMediaDefinition map = Assert.Single(published.Document.Media!.Locations, location => location.Location == "map");
        Assert.Equal(64, map.Bpm);
        Assert.True(map.Loop);
        Assert.Null(published.Document.Media.Locations.Single(location => location.Location == "journal").AmbienceUrl);
        Assert.Equal("gameOver.title", published.Document.Media.GameOver?.LabelKey);
    }

    [Fact]
    public async Task DefaultConfigurationDeclaresLocationsWithoutAnyAsset()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView created = await service.UpsertAsync(
            "default",
            null,
            ConfigurationService.CreateDefault("default"),
            CancellationToken.None);

        MediaDefinition media = Assert.IsType<MediaDefinition>(created.Document.Media);
        Assert.True(media.DefaultMuted);
        Assert.Contains(media.Locations, static location => location.Location == "home");
        Assert.All(media.Locations, static location =>
        {
            Assert.Null(location.AmbienceUrl);
            Assert.Null(location.MusicUrl);
        });
    }

    [Fact]
    public async Task MediaRejectAnInsecureLocationAsset()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("default") with
        {
            Media = new MediaDefinition(
                true,
                true,
                [new AppLocationMediaDefinition("home", "http://insecure.example/ambience.ogg")],
                null),
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, document, CancellationToken.None));

        Assert.Equal("invalid_media", exception.Code);
    }

    [Fact]
    public async Task MediaRejectAnInsecureGameOverAsset()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("default") with
        {
            Media = new MediaDefinition(true, true, [], new GameOverMediaDefinition("http://insecure.example/theme.ogg")),
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, document, CancellationToken.None));

        Assert.Equal("invalid_media", exception.Code);
    }

    [Fact]
    public async Task MediaRejectADuplicatedLocationAndAnOutOfRangeTempo()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");

        ConfigurationException duplicate = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                baseline with
                {
                    Media = new MediaDefinition(
                        true,
                        true,
                        [new AppLocationMediaDefinition("home"), new AppLocationMediaDefinition("Home")],
                        null),
                },
                CancellationToken.None));

        ConfigurationException tempo = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                baseline with
                {
                    Media = new MediaDefinition(true, true, [new AppLocationMediaDefinition("home", Bpm: 300)], null),
                },
                CancellationToken.None));

        Assert.Equal("invalid_media", duplicate.Code);
        Assert.Equal("invalid_media", tempo.Code);
    }

    private sealed class ConfigurationRepositoryStub : IConfigurationRepository
    {
        private ExperienceConfiguration? configuration;
        public Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task AddAsync(ExperienceConfiguration value, CancellationToken cancellationToken) { configuration = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}