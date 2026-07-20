namespace GenEngine.Configuration.Application;

/// <summary>
/// The natures of thing a conditional reward may grant.
/// </summary>
/// <remarks>
/// Deliberately a closed enum rather than a bag of strings. The three natures do not
/// behave alike — an achievement and a title are inert marks a client renders, a currency
/// grant credits the wallet and moves a balance — and a client must be able to tell them
/// apart without parsing a convention. A bag of strings would also have made the amount
/// meaningful for one nature and meaningless for the other two with nothing to say so.
/// </remarks>
public enum RewardGrantType
{
    /// <summary>A named feat, recorded on the profile and rendered as a badge.</summary>
    Achievement,

    /// <summary>A name the player may wear. Purely declarative.</summary>
    Title,

    /// <summary>An amount of the instance currency, credited to the wallet.</summary>
    Currency,
}

/// <summary>
/// One thing a reward grants.
/// </summary>
/// <param name="Type">Which nature this grant has. Decides which other fields are read.</param>
/// <param name="Reference">
/// Stable slug of the achievement or title. It is the join between the configuration and
/// whatever a client renders, so it is never translated. Unused by <see cref="RewardGrantType.Currency"/>.
/// </param>
/// <param name="Label">What the player reads. Translated freely, unlike <paramref name="Reference"/>.</param>
/// <param name="Amount">Points credited. Required by <see cref="RewardGrantType.Currency"/> and read by nothing else.</param>
public sealed record RewardGrantDefinition(
    RewardGrantType Type,
    string Label,
    string? Reference = null,
    int? Amount = null);

/// <summary>
/// One conditional reward: a set of conditions and what crossing them grants.
/// </summary>
/// <remarks>
/// The conditions are the <em>same</em> <see cref="ProgressConditionDefinition"/> the
/// finale uses. A reward is, in the end, a finale that does not end the game: same
/// question asked of the same progression, same <c>All</c>/<c>Any</c> composition, same
/// per-condition progress shown to the player.
/// <para>
/// Like the finale, earning a reward is a <em>crossed threshold</em>: it is stamped once,
/// never re-dated, and locks nothing. There is deliberately no flag making a reward
/// revocable — an operator must not be able to take back what a player earned by editing
/// a document.
/// </para>
/// </remarks>
public sealed record ConditionalRewardDefinition(
    Guid Id,
    bool Enabled,
    string Label,
    string Description,
    ProgressConditionMode Mode,
    IReadOnlyList<ProgressConditionDefinition> Conditions,
    IReadOnlyList<RewardGrantDefinition> Grants,
    string? VisualUrl = null,
    string? LabelKey = null);

/// <summary>
/// Per-instance catalogue of conditional rewards.
/// </summary>
/// <remarks>
/// <see cref="Enabled"/> is the documented disabled behaviour: with it false the
/// catalogue stays published and every reward already earned stays visible, but no new
/// one is ever stamped. Turning rewards off never erases what a player earned.
/// </remarks>
public sealed record RewardsDefinition(
    bool Enabled,
    IReadOnlyList<ConditionalRewardDefinition> Rewards);

/// <summary>Defaults and limits of the <c>rewards</c> block.</summary>
public static class RewardCatalog
{
    public const int MaximumRewards = 48;

    public const int MaximumGrantsPerReward = 6;

    public const int MaximumLabelLength = 120;

    public const int MaximumDescriptionLength = 500;

    public const int MaximumReferenceLength = 60;

    /// <summary>
    /// Highest currency amount a single grant may carry. It shares the player statistic
    /// ceiling for the same reason: an unbounded amount is a typo away from making the
    /// wallet meaningless.
    /// </summary>
    public const int MaximumGrantAmount = 1_000_000;

    /// <summary>
    /// The block a document without one normalizes to: enabled, and empty. Materialised
    /// like <c>media</c> and <c>playerStats</c> so every published document carries the
    /// same shape, but deliberately not opinionated — inventing achievements for an
    /// instance that never asked for them would change what its players see.
    /// </summary>
    public static RewardsDefinition CreateDefault() => new(true, []);

    public static Guid DiapasonPersistanceRewardId { get; } = Guid.Parse("c4e19a55-3f27-4d8b-9a61-72d0b5e3f101");

    /// <summary>
    /// The reward of the Diapason reference configuration, so the shipped instance
    /// demonstrates the capability instead of only declaring it. It is the product
    /// request as it was made: five scenarios closed grants a feat, a title and currency.
    /// </summary>
    /// <remarks>
    /// Its single condition counts scenarios rather than reading a player statistic, for
    /// the same reason <see cref="FinaleCatalog.CreateDiapasonDefault"/> targets a
    /// category rather than a journey. A <c>PlayerStatReached</c> condition is validated
    /// against the keys the very same document publishes in <c>playerStats</c>, so
    /// shipping one here would make any re-authoring of the statistics fail validation
    /// with an error about the rewards — a perfectly legitimate edit blocked by a default
    /// nobody asked for. <c>PlayerStatReached</c> stays fully supported and is covered by
    /// its own tests; it is simply not something the reference configuration pins.
    /// </remarks>
    public static RewardsDefinition CreateDiapasonDefault() => new(
        true,
        [
            new ConditionalRewardDefinition(
                DiapasonPersistanceRewardId,
                true,
                "Cinq fois plutôt qu'une",
                "Vous avez traversé cinq situations complètes sans en abandonner une seule en chemin.",
                ProgressConditionMode.All,
                [
                    new ProgressConditionDefinition(
                        Guid.Parse("c4e19a55-3f27-4d8b-9a61-72d0b5e3f111"),
                        ProgressConditionType.ScenariosCompleted,
                        "Avoir terminé au moins cinq scénarios, quels qu'ils soient.",
                        Threshold: 5),
                ],
                [
                    new RewardGrantDefinition(RewardGrantType.Achievement, "Cinq fois plutôt qu'une", "cinq-scenarios"),
                    new RewardGrantDefinition(RewardGrantType.Title, "Celui qui va au bout", "celui-qui-va-au-bout"),
                    new RewardGrantDefinition(RewardGrantType.Currency, "Cinq scénarios terminés", Amount: 100),
                ],
                LabelKey: "rewards.cinqScenarios"),
        ]);
}