namespace GenEngine.Configuration.Application;

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
    ProgressConditionMode Mode,
    IReadOnlyList<ProgressConditionDefinition> Conditions,
    string? VisualUrl = null,
    string? MusicUrl = null,
    string? LabelKey = null);

/// <summary>Defaults and limits of the finale block.</summary>
public static class FinaleCatalog
{
    /// <summary>
    /// Delegated to <see cref="ProgressConditionCatalog"/>: the finale and the rewards
    /// share one condition model, so they share its bounds too.
    /// </summary>
    public const int MaximumConditions = ProgressConditionCatalog.MaximumConditions;

    public static Guid DiapasonFinaleId { get; } = Guid.Parse("5f2c8b41-7d10-4a63-9e58-3c17a4b6d201");

    /// <summary>
    /// The Diapason finale: the last posture mastered, and most of the ten scenarios
    /// played.
    /// </summary>
    /// <remarks>
    /// The content-referencing condition targets a <em>category</em> rather than a
    /// journey, even though "Ce qui reste après toi" would read more naturally. Journeys
    /// are explicitly meant to be recomposed by each client over the six postures — see
    /// <c>specs/domain/diapason/</c> — so pinning the shipped default to one would make
    /// a perfectly legitimate re-authoring of the journeys fail validation with an error
    /// about the finale. The six categories are the stable axis. <c>JourneyCompleted</c>
    /// stays fully supported for a configuration that does pin its own journeys.
    /// </remarks>
    public static FinaleDefinition CreateDiapasonDefault() => new(
        DiapasonFinaleId,
        true,
        "Ce qui reste après vous",
        "Vous avez traversé les six postures. Ce que vous en gardez vous appartient.",
        "Vous n'avez pas gagné, et vous n'avez rien perdu non plus. Vous avez décidé dix fois sans pouvoir tout vérifier, et chaque décision a laissé une trace lisible par quelqu'un d'autre. Le Diapason continue : les scénarios déjà joués gardent des branches que vous n'avez pas ouvertes.",
        ProgressConditionMode.All,
        [
            new ProgressConditionDefinition(
                Guid.Parse("5f2c8b41-7d10-4a63-9e58-3c17a4b6d211"),
                ProgressConditionType.CategoryCompleted,
                "Avoir terminé la posture « Autonomie ».",
                CategoryId: DiapasonIds.Autonomie),
            new ProgressConditionDefinition(
                Guid.Parse("5f2c8b41-7d10-4a63-9e58-3c17a4b6d212"),
                ProgressConditionType.ScenariosCompleted,
                "Avoir terminé au moins huit scénarios, quels qu'ils soient.",
                Threshold: 8),
        ],
        LabelKey: "finale.title");
}