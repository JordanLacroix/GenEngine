using System.Text.Json;
using System.Text.Json.Serialization;

using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;

namespace GenEngine.Services.Tests;

public sealed class ConfigurationServiceTests
{
    /// <summary>Mirrors the API serializer, so the asserted payload is the served one.</summary>
    private static readonly JsonSerializerOptions PayloadOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    [Fact]
    public async Task PublishedViewRedactsSensitiveOperatorDataAndKeepsThePlayableCatalog()
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

        // Still served: everything a player-facing client or an interservice
        // consumer legitimately reads from the anonymous route.
        Assert.Equal(OrganizationType.Company, published.Document.OrganizationType);
        Assert.Equal(AuthenticationMode.Cumulative, published.Document.Authentication.Mode);
        Assert.True(published.Document.Authentication.EntraEnabled);
        Assert.Equal("Studio", published.Document.Language?.Labels["nav.studio"]);
        Assert.NotEmpty(published.Document.Categories);
        Assert.NotEmpty(published.Document.Familiars);
        Assert.NotEmpty(published.Document.AiProviders);
        Assert.Contains(published.Document.Economy.RewardRules, static rule => rule.Trigger == "ScenarioCompleted");

        // Never served again: tenant identifiers, provider endpoints and the
        // internal organization structure.
        Assert.Null(published.Document.Organization);
        Assert.Empty(published.Document.Assignments!);
        Assert.Null(published.Document.Authentication.EntraTenantId);
        Assert.Null(published.Document.Authentication.EntraClientId);
        Assert.All(published.Document.AiProviders, static provider =>
        {
            Assert.Null(provider.SecretReference);
            Assert.Equal(string.Empty, provider.Endpoint);
            Assert.Equal(string.Empty, provider.Authentication);
        });
    }

    [Fact]
    public async Task AdminViewKeepsTheCompleteDocumentTheAnonymousRouteNoLongerServes()
    {
        var repository = new ConfigurationRepositoryStub();
        var service = new ConfigurationService(repository, TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("company-demo") with
        {
            Authentication = new AuthenticationDefinition(AuthenticationMode.Cumulative, true, true, "tenant-id", "client-id"),
        };

        ExperienceConfigurationView created = await service.UpsertAsync("company-demo", null, document, CancellationToken.None);
        await service.PublishAsync("company-demo", created.Revision, CancellationToken.None);
        ExperienceConfigurationView admin = await service.GetAdminAsync("company-demo", CancellationToken.None);

        Assert.Equal("tenant-id", admin.Document.Authentication.EntraTenantId);
        Assert.Equal("client-id", admin.Document.Authentication.EntraClientId);
        Assert.NotNull(admin.Document.Organization);
        Assert.NotEmpty(admin.Document.Organization!.Units);
        Assert.Contains(admin.Document.AiProviders, static provider => provider.SecretReference == "azure-foundry-credential");
        Assert.Contains(admin.Document.AiProviders, static provider => provider.Endpoint.StartsWith("https://", StringComparison.Ordinal));
    }

    [Fact]
    public async Task ClientBootstrapLeaksNoEntraIdentifierAndNoOrganizationStructure()
    {
        var repository = new ConfigurationRepositoryStub();
        var service = new ConfigurationService(repository, TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("default") with
        {
            Authentication = new AuthenticationDefinition(AuthenticationMode.Cumulative, true, true, "tenant-id", "client-id"),
        };

        ExperienceConfigurationView created = await service.UpsertAsync("default", null, document, CancellationToken.None);
        await service.PublishAsync("default", created.Revision, CancellationToken.None);
        ClientBootstrapView bootstrap = await service.GetClientBootstrapAsync("default", CancellationToken.None);

        Assert.Equal("Le Diapason", bootstrap.ApplicationName);
        Assert.Equal("Diapason", bootstrap.ShortName);
        Assert.Equal("fr-FR", bootstrap.Locale);
        Assert.Equal("Europe/Paris", bootstrap.TimeZone);
        Assert.Equal(AuthenticationMode.Cumulative, bootstrap.AuthenticationMode);
        Assert.True(bootstrap.DemoEnabled);
        Assert.Equal("Carte", bootstrap.Labels["nav.map"]);
        Assert.NotNull(bootstrap.Intro);
        Assert.Equal(1, bootstrap.Version);

        // Serialized shape is the real surface: assert on the payload a client
        // receives, not only on the record, so an added property cannot leak.
        string payload = JsonSerializer.Serialize(bootstrap, PayloadOptions);
        using JsonDocument served = JsonDocument.Parse(payload);
        Assert.Equal(
            [
                "frontId", "version", "publishedAt", "applicationName", "shortName", "tagline",
                "branding", "locale", "timeZone", "labels", "authenticationMode", "demoEnabled", "intro",
            ],
            served.RootElement.EnumerateObject().Select(static property => property.Name).ToArray());
        Assert.Equal("#d7a746", served.RootElement.GetProperty("branding").GetProperty("theme").GetProperty("colors").GetProperty("accent").GetString());
        Assert.Equal("#17344a", served.RootElement.GetProperty("branding").GetProperty("accentPalette").GetProperty("encre").GetString());
        Assert.Equal("Cumulative", served.RootElement.GetProperty("authenticationMode").GetString());

        foreach (string forbidden in new[]
                 {
                     "tenant-id", "client-id", "entraTenantId", "entraClientId",
                     "organization", "assignments", "aiProviders", "categories",
                     "journeys", "economy", "modules", "familiars", "secretReference",
                 })
        {
            Assert.DoesNotContain(forbidden, payload, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task AConfigurationWithoutBrandingStaysPublishableAndReadable()
    {
        var repository = new ConfigurationRepositoryStub();
        var service = new ConfigurationService(repository, TimeProvider.System);
        ExperienceDocument document = ConfigurationService.CreateDefault("legacy") with { Branding = null };

        ExperienceConfigurationView created = await service.UpsertAsync("legacy", null, document, CancellationToken.None);
        await service.PublishAsync("legacy", created.Revision, CancellationToken.None);
        PublishedExperienceView published = await service.GetPublishedAsync("legacy", CancellationToken.None);
        ClientBootstrapView bootstrap = await service.GetClientBootstrapAsync("legacy", CancellationToken.None);

        Assert.Null(created.Document.Branding);
        Assert.Null(published.Document.Branding);
        Assert.Null(bootstrap.Branding);
        Assert.Null(bootstrap.ShortName);
        // The client falls back on the game name; "GenEngine" remains the client-side default.
        Assert.Equal("Le Diapason", bootstrap.ApplicationName);
    }

    [Fact]
    public async Task DefaultBrandingMapsEveryAccentUsedByTheCatalog()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");

        ExperienceConfigurationView created = await service.UpsertAsync("default", null, baseline, CancellationToken.None);

        BrandingDefinition branding = Assert.IsType<BrandingDefinition>(created.Document.Branding);
        Assert.Equal("Le Diapason", branding.ApplicationName);
        Assert.Null(branding.BrandIconUrl);
        Assert.Null(branding.LogoUrl);
        Assert.Null(branding.FaviconUrl);
        Assert.Equal(BrandingColorScheme.Light, branding.Theme!.ColorScheme);
        Assert.All(ConfigurationService.RequiredThemeColors, token => Assert.True(branding.Theme.Colors.ContainsKey(token)));

        IReadOnlyDictionary<string, string> palette = Assert.IsAssignableFrom<IReadOnlyDictionary<string, string>>(branding.AccentPalette);
        foreach (string accent in created.Document.Categories.Select(category => category.Accent)
                     .Concat(created.Document.Journeys!.Select(journey => journey.Accent))
                     .Concat(created.Document.Familiars.Select(familiar => familiar.Accent)))
        {
            Assert.True(palette.ContainsKey(accent), $"The accent '{accent}' has no colour in the palette.");
        }
    }

    [Fact]
    public async Task BrandingRejectsAColorThatIsNotStrictHexadecimal()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");

        foreach (string invalid in new[] { "#fff", "rebeccapurple", "rgb(23,52,74)", "17344a", "#17344g", "#17344aFF0" })
        {
            ExperienceDocument document = baseline with
            {
                Branding = new BrandingDefinition(AccentPalette: new Dictionary<string, string> { ["encre"] = invalid }),
            };

            ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
                service.UpsertAsync("default", null, document, CancellationToken.None));

            Assert.Equal("invalid_branding", exception.Code);
        }

        // Eight digits stay valid: an accent may carry its alpha channel.
        ExperienceConfigurationView accepted = await service.UpsertAsync(
            "default",
            null,
            baseline with
            {
                Branding = new BrandingDefinition(AccentPalette: new Dictionary<string, string> { ["encre"] = "#17344AFF" }),
            },
            CancellationToken.None);

        Assert.Equal("#17344AFF", accepted.Document.Branding!.AccentPalette!["encre"]);
    }

    [Fact]
    public async Task BrandingRejectsAnInsecureIconAndAcceptsAPackReference()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");

        ConfigurationException insecure = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                baseline with { Branding = new BrandingDefinition(LogoUrl: "http://insecure.example/logo.svg") },
                CancellationToken.None));

        ConfigurationException malformed = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                baseline with { Branding = new BrandingDefinition(FaviconUrl: "Diapason Core:Icon") },
                CancellationToken.None));

        ExperienceConfigurationView accepted = await service.UpsertAsync(
            "default",
            null,
            baseline with
            {
                Branding = new BrandingDefinition(
                    BrandIconUrl: "diapason-core:icon.highlight",
                    ClientIconUrl: "https://assets.example.org/client-icon.svg"),
            },
            CancellationToken.None);

        Assert.Equal("invalid_branding", insecure.Code);
        Assert.Equal("invalid_branding", malformed.Code);
        Assert.Equal("diapason-core:icon.highlight", accepted.Document.Branding!.BrandIconUrl);
    }

    [Fact]
    public async Task BrandingRejectsAThemeMissingARequiredColorToken()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");
        Dictionary<string, string> colors = ConfigurationService.RequiredThemeColors
            .Where(static token => token != "muted")
            .ToDictionary(static token => token, static _ => "#17344a");

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                baseline with { Branding = new BrandingDefinition(Theme: new BrandingThemeDefinition(colors)) },
                CancellationToken.None));

        Assert.Equal("invalid_branding", exception.Code);
    }

    [Fact]
    public async Task BrandingRejectsABlankNameAndAnOutOfRangeCornerRadius()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");
        Dictionary<string, string> colors = ConfigurationService.RequiredThemeColors
            .ToDictionary(static token => token, static _ => "#17344a");

        ConfigurationException blank = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                baseline with { Branding = new BrandingDefinition(ApplicationName: "   ") },
                CancellationToken.None));

        ConfigurationException radius = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync(
                "default",
                null,
                baseline with { Branding = new BrandingDefinition(Theme: new BrandingThemeDefinition(colors, CornerRadius: 128)) },
                CancellationToken.None));

        Assert.Equal("invalid_branding", blank.Code);
        Assert.Equal("invalid_branding", radius.Code);
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