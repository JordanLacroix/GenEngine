namespace GenEngine.Narrative.Tests;

public sealed class NarrativeMediaTests
{
    private const string Visual = "https://assets.example.org/scene-palace-atrium-v1.avif";
    private const string Ambience = "https://assets.example.org/ambience-atrium-v1.ogg";
    private const string Signature = "https://assets.example.org/sfx-ui-choice-confirm-v1.ogg";

    [Fact]
    public void ScenarioWithoutAnyMediaRemainsValidAndPlayable()
    {
        ScenarioDocument scenario = CreateScenario(withMedia: false);

        ValidationReport report = ScenarioValidator.Validate(scenario);
        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, NarrativeRuntime.Start(scenario));

        Assert.True(report.IsValid);
        Assert.Null(step.Media);
        Assert.All(step.Choices, static choice => Assert.Null(choice.Media));
        Assert.Equal("The hall listens.", step.Text);
    }

    [Fact]
    public void StepAndChoiceMediaAreProjectedToTheCurrentStep()
    {
        ScenarioDocument scenario = CreateScenario(withMedia: true);

        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, NarrativeRuntime.Start(scenario));

        Assert.True(ScenarioValidator.Validate(scenario).IsValid);
        Assert.Equal(Visual, step.Media?.VisualUrl);
        Assert.Equal(Ambience, step.Media?.SoundUrl);
        Assert.Equal("Un atrium doré traversé par une lumière haute.", step.Media?.VisualDescription);
        VisibleChoice choice = Assert.Single(step.Choices);
        Assert.Equal(Signature, choice.Media?.SoundUrl);
        Assert.Equal("choice-confirm", choice.Media?.AnimationCue);
    }

    [Fact]
    public void MediaAreProjectedToBothNarrativeMaps()
    {
        ScenarioDocument scenario = CreateScenario(withMedia: true);

        NarrativeTree tree = NarrativeTreeBuilder.Build(scenario, NarrativeRuntime.Start(scenario));
        NarrativeStructure structure = NarrativeTreeBuilder.BuildStructure(scenario);

        Assert.Equal(Visual, tree.Nodes.Single(node => node.Id == "start").Media?.VisualUrl);
        Assert.Equal(Visual, structure.Nodes.Single(node => node.Id == "start").Media?.VisualUrl);
        Assert.Null(structure.Nodes.Single(node => node.Id == "ending").Media);
    }

    [Theory]
    [InlineData("http://assets.example.org/scene.avif")]
    [InlineData("assets/scene.avif")]
    [InlineData("javascript:alert(1)")]
    [InlineData("   ")]
    public void NonHttpsStepAssetsAreRejected(string url)
    {
        ScenarioDocument scenario = CreateScenario(withMedia: true) with { SchemaVersion = NarrativeVersions.MediaSchema };
        scenario = ReplaceStartMedia(scenario, new StepMedia { VisualUrl = url });

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "media_asset_invalid");
    }

    [Fact]
    public void NonHttpsChoiceSoundIsRejected()
    {
        ScenarioDocument scenario = CreateScenario(withMedia: true);
        NarrativeNode start = scenario.Nodes[0];
        scenario = scenario with
        {
            Nodes =
            [
                start with
                {
                    Choices =
                    [
                        start.Choices[0] with { Media = new ChoiceMedia { SoundUrl = "http://assets.example.org/sfx.ogg" } },
                    ],
                },
                scenario.Nodes[1],
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "media_asset_invalid");
    }

    [Fact]
    public void AnimationCueLongerThanTheBudgetIsRejected()
    {
        ScenarioDocument scenario = CreateScenario(withMedia: true);
        NarrativeNode start = scenario.Nodes[0];
        scenario = scenario with
        {
            Nodes =
            [
                start with
                {
                    Choices =
                    [
                        start.Choices[0] with { Media = new ChoiceMedia { AnimationCue = new string('c', 65) } },
                    ],
                },
                scenario.Nodes[1],
            ],
        };

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "media_animation_cue_invalid");
    }

    [Fact]
    public void MediaDeclaredBeforeSchemaThreeAreRejected()
    {
        ScenarioDocument scenario = CreateScenario(withMedia: true) with { SchemaVersion = 2 };

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.False(report.IsValid);
        Assert.Contains(report.Issues, static issue => issue.Code == "media_requires_schema_3");
    }

    [Fact]
    public void TypedInteractionsRemainValidOnASchemaTwoDocument()
    {
        ScenarioDocument scenario = new(
            2,
            "Schema two interactions",
            "start",
            [
                new NarrativeNode("start", "The hall listens.", null, [], [])
                {
                    Interactions =
                    [
                        new ChoiceSetInteraction(
                            "decide",
                            "How do you approach?",
                            [new NarrativeChoice("listen", "Listen", "ending", null, [])]),
                    ],
                },
                new NarrativeNode("ending", "The interval resolves.", null, [], [], true),
            ]);

        ValidationReport report = ScenarioValidator.Validate(scenario);

        Assert.True(report.IsValid);
    }

    [Fact]
    public void DocumentedMediaExampleIsValidAndPlayable()
    {
        string path = Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..",
            "specs", "domain", "examples", "illustrated-atrium-media.json");
        ScenarioDocument scenario = NarrativeJson.Deserialize<ScenarioDocument>(File.ReadAllText(path));

        ValidationReport report = ScenarioValidator.Validate(scenario);
        CurrentStep step = NarrativeRuntime.GetCurrentStep(scenario, NarrativeRuntime.Start(scenario));

        Assert.True(report.IsValid);
        Assert.Equal(NarrativeVersions.MediaSchema, scenario.SchemaVersion);
        Assert.NotNull(step.Media?.VisualDescription);
        Assert.Contains(step.Choices, static choice => choice.Media?.AnimationCue == "choice-confirm");
        Assert.Contains(step.Choices, static choice => choice.Media?.SoundUrl is null);
    }

    private static ScenarioDocument ReplaceStartMedia(ScenarioDocument scenario, StepMedia media) =>
        scenario with { Nodes = [scenario.Nodes[0] with { Media = media }, scenario.Nodes[1]] };

    private static ScenarioDocument CreateScenario(bool withMedia) => new(
        NarrativeVersions.MediaSchema,
        "Media scenario",
        "start",
        [
            new NarrativeNode(
                "start",
                "The hall listens.",
                null,
                [],
                [
                    new NarrativeChoice("listen", "Listen before speaking", "ending", null, [])
                    {
                        Media = withMedia
                            ? new ChoiceMedia { SoundUrl = Signature, AnimationCue = "choice-confirm" }
                            : null,
                    },
                ])
            {
                Media = withMedia
                    ? new StepMedia
                    {
                        VisualUrl = Visual,
                        VisualDescription = "Un atrium doré traversé par une lumière haute.",
                        SoundUrl = Ambience,
                    }
                    : null,
            },
            new NarrativeNode("ending", "The interval resolves.", null, [], [], true),
        ]);
}