namespace GenEngine.Configuration.Application;

/// <summary>
/// One configurable player statistic: a counter the player carries across scenarios,
/// starting at zero and saturating at <paramref name="Maximum"/>.
/// </summary>
/// <param name="Id">Stable identifier. Never reassigned once published.</param>
/// <param name="Key">
/// Short slug written by scenario authors in a <c>grantPlayerStat</c> effect and stored
/// per player. It is the join between the narrative and the profile, so it is a closed
/// charset and never translated.
/// </param>
/// <param name="Label">Displayed name of the stat on the player profile.</param>
/// <param name="Description">What the stat means, written for the player.</param>
/// <param name="Maximum">
/// Ceiling of the stat. A grant that would exceed it saturates rather than failing: an
/// author must never have to know the player's current value to write a working effect.
/// </param>
public sealed record PlayerStatDefinition(
    Guid Id,
    string Key,
    string Label,
    string Description,
    int Maximum);

/// <summary>
/// Per-instance catalogue of player statistics.
/// </summary>
/// <remarks>
/// The block is materialised by normalization like <c>media</c>, so every published
/// document carries it and no client has to special-case its absence. Its default is
/// deliberately <em>empty</em> rather than opinionated: inventing statistics for an
/// instance that never asked for them would change what its players see on their
/// profile. The shipped Diapason configuration declares its own six.
/// <para>
/// <see cref="Enabled"/> is the documented disabled behaviour: with it false the
/// catalogue is still published and readable, existing values are preserved, but no
/// narrative grant is applied. Turning stats off never destroys what a player earned.
/// </para>
/// </remarks>
public sealed record PlayerStatsDefinition(
    bool Enabled,
    IReadOnlyList<PlayerStatDefinition> Stats);

/// <summary>Defaults and limits of the <c>playerStats</c> block.</summary>
public static class PlayerStatCatalog
{
    public const int MaximumStats = 24;

    public const int MaximumKeyLength = 40;

    public const int MaximumLabelLength = 80;

    public const int MaximumDescriptionLength = 500;

    /// <summary>
    /// Highest ceiling an operator may configure. A bound exists so a stat stays a
    /// readable progress bar rather than an unbounded counter, and so the value can
    /// never overflow the integer the profile stores it in.
    /// </summary>
    public const int MaximumCeiling = 1_000_000;

    /// <summary>
    /// The block a document without one normalizes to: enabled, and empty. See the
    /// remarks on <see cref="PlayerStatsDefinition"/> for why nothing is invented.
    /// </summary>
    public static PlayerStatsDefinition CreateDefault() => new(true, []);

    /// <summary>
    /// The six statistics of the Diapason reference configuration, one per posture, so
    /// the shipped instance demonstrates the capability instead of only declaring it.
    /// The keys match the slugs a scenario writes in its <c>grantPlayerStat</c> effects.
    /// </summary>
    public static PlayerStatsDefinition CreateDiapasonDefault() => new(
        true,
        [
            new PlayerStatDefinition(
                Guid.Parse("9a1d4c70-5b2e-4f18-8c33-6e0d17b4a101"),
                "lucidite",
                "Lucidité",
                "Ce que vous avez su voir avant d'interpréter : les fois où vous avez cherché le fait plutôt que l'explication.",
                100),
            new PlayerStatDefinition(
                Guid.Parse("9a1d4c70-5b2e-4f18-8c33-6e0d17b4a102"),
                "discernement",
                "Discernement",
                "Ce que vous avez su trier quand tout était plausible.",
                100),
            new PlayerStatDefinition(
                Guid.Parse("9a1d4c70-5b2e-4f18-8c33-6e0d17b4a103"),
                "arbitrage",
                "Arbitrage",
                "Les décisions que vous avez assumées sous contrainte, en sachant ce que vous perdiez.",
                100),
            new PlayerStatDefinition(
                Guid.Parse("9a1d4c70-5b2e-4f18-8c33-6e0d17b4a104"),
                "courage",
                "Courage",
                "Les fois où vous avez parlé, refusé ou signalé alors que c'était coûteux.",
                100),
            new PlayerStatDefinition(
                Guid.Parse("9a1d4c70-5b2e-4f18-8c33-6e0d17b4a105"),
                "transmission",
                "Transmission",
                "Ce que vous avez rendu utilisable par quelqu'un d'autre que vous.",
                100),
            new PlayerStatDefinition(
                Guid.Parse("9a1d4c70-5b2e-4f18-8c33-6e0d17b4a106"),
                "autonomie",
                "Autonomie",
                "Les compétences que vous avez gardées alors que vous auriez pu les déléguer.",
                100),
        ]);
}