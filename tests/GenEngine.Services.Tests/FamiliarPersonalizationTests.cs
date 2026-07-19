using GenEngine.Configuration.Application;
using GenEngine.Configuration.Domain;
using GenEngine.PlayerExperience.Application;
using GenEngine.PlayerExperience.Domain;

namespace GenEngine.Services.Tests;

/// <summary>
/// The familiar is personalisable along catalogued axes only. These tests pin the two
/// properties that make the axes previewable: every value comes from the published
/// catalogue, and a configuration or a profile written before the axes existed keeps
/// working.
/// </summary>
public sealed class FamiliarPersonalizationTests
{
    private static readonly Guid FamiliarId = Guid.Parse("04b758d1-862d-4f01-b2c9-d7f5ccf33a0f");

    [Fact]
    public async Task PublishedFamiliarsExposeEveryPersonalisationAxisWithRenderableOptions()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView created = await service.UpsertAsync(
            "default", null, ConfigurationService.CreateDefault("default"), CancellationToken.None);

        FamiliarDefinition familiar = Assert.Single(created.Document.Familiars);
        IReadOnlyList<FamiliarAxisDefinition> axes = Assert.IsAssignableFrom<IReadOnlyList<FamiliarAxisDefinition>>(familiar.Axes);

        Assert.Equal(FamiliarAxes.All.Order(), axes.Select(axis => axis.Axis).Order());
        Assert.All(axes, axis =>
        {
            Assert.False(string.IsNullOrWhiteSpace(axis.Label));
            Assert.NotEmpty(axis.Options);
            // Every option must carry what a client needs to render and explain it,
            // which is exactly what a free-text axis could never provide.
            Assert.All(axis.Options, option =>
            {
                Assert.False(string.IsNullOrWhiteSpace(option.Value));
                Assert.False(string.IsNullOrWhiteSpace(option.Label));
                Assert.False(string.IsNullOrWhiteSpace(option.Description));
            });
            Assert.Contains(axis.Options, option => option.Value == axis.DefaultValue);
        });

        // The two axes that used to be free text are now catalogued.
        Assert.Contains(axes, axis => axis.Axis == FamiliarAxes.WritingStyle && axis.Options.Count > 1);
        Assert.Contains(axes, axis => axis.Axis == FamiliarAxes.Accent && axis.Options.Count > 1);
    }

    [Fact]
    public async Task TheLegacyFormAndToneListsStayDerivedFromTheirAxis()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);

        ExperienceConfigurationView created = await service.UpsertAsync(
            "default", null, ConfigurationService.CreateDefault("default"), CancellationToken.None);

        FamiliarDefinition familiar = Assert.Single(created.Document.Familiars);
        FamiliarAxisDefinition forms = familiar.Axes!.Single(axis => axis.Axis == FamiliarAxes.Form);

        Assert.Equal(forms.Options.Select(option => option.Value), familiar.AvailableForms);
    }

    [Fact]
    public async Task AConfigurationWithoutAxesStillPublishesTheValuesItAlreadyUsed()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("legacy");
        // A document as written before the axes existed: two lists, and free text for
        // the writing style and the accent.
        ExperienceDocument document = baseline with
        {
            Familiars = baseline.Familiars
                .Select(familiar => familiar with
                {
                    Axes = null,
                    Form = "spark",
                    Tone = "Warm",
                    WritingStyle = "MaisonStyle",
                    Accent = "vermillon",
                    AvailableForms = ["spark", "owl"],
                    AvailableTones = ["Warm", "Direct"],
                })
                .ToArray(),
        };

        ExperienceConfigurationView created = await service.UpsertAsync("legacy", null, document, CancellationToken.None);

        FamiliarDefinition familiar = Assert.Single(created.Document.Familiars);
        Assert.Equal(["spark", "owl"], familiar.AvailableForms);
        // The values already in use are preserved as options, otherwise a profile that
        // had legitimately chosen them would become invalid.
        Assert.Contains(familiar.Axes!.Single(axis => axis.Axis == FamiliarAxes.WritingStyle).Options, option => option.Value == "MaisonStyle");
        Assert.Contains(familiar.Axes!.Single(axis => axis.Axis == FamiliarAxes.Accent).Options, option => option.Value == "vermillon");
    }

    [Fact]
    public async Task AnAxisWhoseDefaultIsNotAnOptionIsRejected()
    {
        var service = new ConfigurationService(new ConfigurationRepositoryStub(), TimeProvider.System);
        ExperienceDocument baseline = ConfigurationService.CreateDefault("default");
        ExperienceDocument document = baseline with
        {
            Familiars = baseline.Familiars
                .Select(familiar => familiar with
                {
                    Axes =
                    [
                        new FamiliarAxisDefinition(FamiliarAxes.Aura, "Aura", "", "inexistante",
                            [new FamiliarOptionDefinition("halo", "Halo", "Un cercle net.")]),
                    ],
                })
                .ToArray(),
        };

        ConfigurationException exception = await Assert.ThrowsAsync<ConfigurationException>(() =>
            service.UpsertAsync("default", null, document, CancellationToken.None));

        Assert.Equal("invalid_familiar_axis", exception.Code);
    }

    [Fact]
    public async Task APlayerCanSelectEveryAxisAndTheChoiceIsPersisted()
    {
        var service = new PlayerExperienceService(new RepositoryStub(), new AxisCatalogStub(), TimeProvider.System);
        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);

        PlayerExperienceView updated = await service.ConfigureFamiliarAsync(
            "player-1",
            "default",
            new FamiliarSelection(FamiliarId, "owl", "Direct", "Concise", "sauge", 3, "Tierce", 1, false, new Dictionary<string, string>
            {
                ["aura"] = "glow",
                ["silhouette"] = "compact",
                ["speechRhythm"] = "slow",
                ["languageRegister"] = "formal",
                ["interventionDensity"] = "sparse",
            }),
            current.Revision,
            CancellationToken.None);

        Assert.Equal("owl", updated.Familiar?.Form);
        Assert.Equal("Concise", updated.Familiar?.WritingStyle);
        Assert.Equal("sauge", updated.Familiar?.Accent);
        Assert.Equal("glow", updated.Familiar?.Axes?["aura"]);
        Assert.Equal("formal", updated.Familiar?.Axes?["languageRegister"]);
        Assert.Equal("sparse", updated.Familiar?.Axes?["interventionDensity"]);
    }

    [Fact]
    public async Task AValueOutsideTheCatalogueIsRefused()
    {
        var service = new PlayerExperienceService(new RepositoryStub(), new AxisCatalogStub(), TimeProvider.System);
        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);

        // The writing style used to be accepted as free text. It no longer is.
        PlayerExperienceException style = await Assert.ThrowsAsync<PlayerExperienceException>(() =>
            service.ConfigureFamiliarAsync(
                "player-1", "default",
                new FamiliarSelection(FamiliarId, "owl", "Direct", "StyleInvente", "sauge", 3),
                current.Revision, CancellationToken.None));

        PlayerExperienceException accent = await Assert.ThrowsAsync<PlayerExperienceException>(() =>
            service.ConfigureFamiliarAsync(
                "player-1", "default",
                new FamiliarSelection(FamiliarId, "owl", "Direct", "Concise", "fuchsia-perso", 3),
                current.Revision, CancellationToken.None));

        PlayerExperienceException aura = await Assert.ThrowsAsync<PlayerExperienceException>(() =>
            service.ConfigureFamiliarAsync(
                "player-1", "default",
                new FamiliarSelection(FamiliarId, "owl", "Direct", "Concise", "sauge", 3, Axes: new Dictionary<string, string> { ["aura"] = "supernova" }),
                current.Revision, CancellationToken.None));

        Assert.Equal("invalid_familiar_configuration", style.Code);
        Assert.Equal("invalid_familiar_configuration", accent.Code);
        Assert.Equal("invalid_familiar_configuration", aura.Code);
    }

    [Fact]
    public async Task AnUndeclaredAxisIsRefusedRatherThanSilentlyStored()
    {
        var service = new PlayerExperienceService(new RepositoryStub(), new AxisCatalogStub(), TimeProvider.System);
        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);

        PlayerExperienceException exception = await Assert.ThrowsAsync<PlayerExperienceException>(() =>
            service.ConfigureFamiliarAsync(
                "player-1", "default",
                new FamiliarSelection(FamiliarId, "owl", "Direct", "Concise", "sauge", 3, Axes: new Dictionary<string, string> { ["parfum"] = "cèdre" }),
                current.Revision, CancellationToken.None));

        Assert.Equal("unknown_familiar_axis", exception.Code);
    }

    [Fact]
    public async Task AnUnansweredAxisFallsBackToItsDefaultSoAnOlderClientKeepsWorking()
    {
        var service = new PlayerExperienceService(new RepositoryStub(), new AxisCatalogStub(), TimeProvider.System);
        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);

        // Exactly the payload a client written before the axes existed sends.
        PlayerExperienceView updated = await service.ConfigureFamiliarAsync(
            "player-1", "default",
            new FamiliarSelection(FamiliarId, "owl", "Direct", "Concise", "sauge", 3),
            current.Revision, CancellationToken.None);

        Assert.Equal("none", updated.Familiar?.Axes?["aura"]);
        Assert.Equal("standard", updated.Familiar?.Axes?["silhouette"]);
        Assert.Equal("measured", updated.Familiar?.Axes?["speechRhythm"]);
    }

    [Fact]
    public async Task TheCustomNameCannotCarryActiveContent()
    {
        var service = new PlayerExperienceService(new RepositoryStub(), new AxisCatalogStub(), TimeProvider.System);
        PlayerExperienceView current = await service.GetAsync("player-1", "default", CancellationToken.None);

        PlayerExperienceDomainException markup = await Assert.ThrowsAsync<PlayerExperienceDomainException>(() =>
            service.ConfigureFamiliarAsync(
                "player-1", "default",
                new FamiliarSelection(FamiliarId, "owl", "Direct", "Concise", "sauge", 3, "<script>alert(1)</script>"),
                current.Revision, CancellationToken.None));

        PlayerExperienceDomainException tooLong = await Assert.ThrowsAsync<PlayerExperienceDomainException>(() =>
            service.ConfigureFamiliarAsync(
                "player-1", "default",
                new FamiliarSelection(FamiliarId, "owl", "Direct", "Concise", "sauge", 3, new string('a', 81)),
                current.Revision, CancellationToken.None));

        Assert.Equal("invalid_custom_name", markup.Code);
        Assert.Equal("invalid_custom_name", tooLong.Code);
    }

    private sealed class ConfigurationRepositoryStub : IConfigurationRepository
    {
        private ExperienceConfiguration? configuration;
        public Task<ExperienceConfiguration?> GetAsync(string frontId, CancellationToken cancellationToken) => Task.FromResult(configuration);
        public Task AddAsync(ExperienceConfiguration value, CancellationToken cancellationToken) { configuration = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RepositoryStub : IPlayerExperienceRepository
    {
        private PlayerProfile? profile;
        public Task<PlayerProfile?> GetAsync(string userId, string frontId, CancellationToken cancellationToken) => Task.FromResult(profile);
        public Task AddAsync(PlayerProfile value, CancellationToken cancellationToken) { profile = value; return Task.CompletedTask; }
        public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>A published catalogue carrying the full axis set, as Configuration now serves it.</summary>
    private sealed class AxisCatalogStub : IPlayerExperienceCatalogProvider
    {
        public Task<PlayerExperienceCatalog> GetAsync(string frontId, CancellationToken cancellationToken) =>
            Task.FromResult(new PlayerExperienceCatalog(
                "ACCORD", "Accords", "♪", 0,
                [
                    new FamiliarOption(FamiliarId, "Tierce", "Une voix.", "spark", "Warm", "Socratic", "amber", 2, ["hint"], ["spark", "owl"], ["Warm", "Direct"], null, null, null, null, null,
                    [
                        Axis("form", "spark", ("spark", "Étincelle"), ("owl", "Chouette")),
                        Axis("tone", "Warm", ("Warm", "Chaleureux"), ("Direct", "Direct")),
                        Axis("writingStyle", "Socratic", ("Socratic", "Socratique"), ("Concise", "Concis")),
                        Axis("accent", "amber", ("amber", "Ambre"), ("sauge", "Sauge")),
                        Axis("aura", "none", ("none", "Aucune"), ("glow", "Lueur")),
                        Axis("silhouette", "standard", ("standard", "Standard"), ("compact", "Compacte")),
                        Axis("speechRhythm", "measured", ("measured", "Mesuré"), ("slow", "Lent")),
                        Axis("languageRegister", "standard", ("standard", "Courant"), ("formal", "Soutenu")),
                        Axis("interventionDensity", "regular", ("regular", "Régulier"), ("sparse", "Rare")),
                    ]),
                ],
                [new RewardRule("ScenarioCompleted", "*", 25, "Terminer un scénario")],
                [],
                new OnboardingTutorial(Guid.NewGuid(), 1, true, true, false, []),
                new AssistantPolicy(true, true, true, true, 2, ["hint"])));

        private static FamiliarAxis Axis(string key, string defaultValue, params (string Value, string Label)[] options) =>
            new(key, key, string.Empty, defaultValue,
                options.Select((option, index) => new FamiliarAxisOption(option.Value, option.Label, "Effet décrit.", null, null, index + 1)).ToArray());
    }
}