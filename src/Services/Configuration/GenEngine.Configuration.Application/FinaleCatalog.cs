namespace GenEngine.Configuration.Application;

/// <summary>
/// The kinds of trigger a finale may declare. Each one is answerable from the
/// cross-session mastery already recorded per (profile, scenario version), so no
/// second progression store is introduced.
/// </summary>
public enum FinaleConditionType
{
    /// <summary>At least <c>Threshold</c> distinct scenarios completed, optionally restricted to <c>ScenarioIds</c>.</summary>
    ScenariosCompleted,

    /// <summary>Every scenario attached to <c>CategoryId</c> completed.</summary>
    CategoryCompleted,

    /// <summary>Every category of <c>JourneyId</c> completed.</summary>
    JourneyCompleted,

    /// <summary>At least <c>Threshold</c> of the endings listed in <c>EndingIds</c> reached.</summary>
    EndingsReached,

    /// <summary>Average mastery over the scenarios in scope reaches <c>Threshold</c> percent.</summary>
    MasteryPercentReached,
}

/// <summary>Whether every condition must hold, or any single one is enough.</summary>
public enum FinaleConditionMode { All, Any }

/// <summary>
/// One composable trigger of the finale. Only the fields its <see cref="Type"/> uses
/// are read; the others stay null so the document remains additive.
/// </summary>
public sealed record FinaleConditionDefinition(
    Guid Id,
    FinaleConditionType Type,
    string Description,
    int? Threshold = null,
    Guid? CategoryId = null,
    Guid? JourneyId = null,
    IReadOnlyList<string>? EndingIds = null,
    IReadOnlyList<Guid>? ScenarioIds = null);

/// <summary>
/// A global end-of-game scene, triggered by composable conditions evaluated
/// deterministically against the player's recorded mastery.
/// </summary>
/// <remarks>
/// Reaching the finale is a <em>crossed threshold</em>, never a terminal state: it is
/// stamped once on the profile and nothing is locked afterwards. There is deliberately
/// no flag to make it locking — an operator must not be able to configure a dead end,
/// so the guarantee lives in the model rather than in a default value.
/// </remarks>
public sealed record FinaleDefinition(
    Guid Id,
    bool Enabled,
    string Title,
    string Summary,
    string Body,
    FinaleConditionMode Mode,
    IReadOnlyList<FinaleConditionDefinition> Conditions,
    string? VisualUrl = null,
    string? MusicUrl = null,
    string? LabelKey = null);

/// <summary>Defaults and limits of the finale block.</summary>
public static class FinaleCatalog
{
    public const int MaximumConditions = 12;

    public static Guid DiapasonFinaleId { get; } = Guid.Parse("5f2c8b41-7d10-4a63-9e58-3c17a4b6d201");

    /// <summary>
    /// The Diapason finale: the third journey closed, the ten scenarios played, and
    /// the two endings that state what the player chose to keep.
    /// </summary>
    public static FinaleDefinition CreateDiapasonDefault() => new(
        DiapasonFinaleId,
        true,
        "Ce qui reste après vous",
        "Vous avez traversé les six postures. Ce que vous en gardez vous appartient.",
        "Vous n'avez pas gagné, et vous n'avez rien perdu non plus. Vous avez décidé dix fois sans pouvoir tout vérifier, et chaque décision a laissé une trace lisible par quelqu'un d'autre. Le Diapason continue : les scénarios déjà joués gardent des branches que vous n'avez pas ouvertes.",
        FinaleConditionMode.All,
        [
            new FinaleConditionDefinition(
                Guid.Parse("5f2c8b41-7d10-4a63-9e58-3c17a4b6d211"),
                FinaleConditionType.JourneyCompleted,
                "Avoir terminé le parcours « Ce qui reste après toi ».",
                JourneyId: DiapasonIds.CeQuiReste),
            new FinaleConditionDefinition(
                Guid.Parse("5f2c8b41-7d10-4a63-9e58-3c17a4b6d212"),
                FinaleConditionType.ScenariosCompleted,
                "Avoir terminé au moins huit scénarios, quels qu'ils soient.",
                Threshold: 8),
        ],
        LabelKey: "finale.title");
}