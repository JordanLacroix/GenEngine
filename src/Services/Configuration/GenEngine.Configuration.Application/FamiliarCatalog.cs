namespace GenEngine.Configuration.Application;

/// <summary>
/// One selectable value of a familiar personalisation axis.
/// </summary>
/// <remarks>
/// Every axis value is catalogued rather than free text, so a client can preview it
/// before the player commits: <see cref="Label"/> names it, <see cref="Description"/>
/// states what it changes, <see cref="AccentToken"/> carries the colour or accent token
/// the client renders with, and <see cref="AssetReference"/> optionally points at an
/// illustration. A free-text axis could carry none of this, which is exactly why
/// <c>writingStyle</c> and <c>accent</c> moved into the catalogue.
/// </remarks>
public sealed record FamiliarOptionDefinition(
    string Value,
    string Label,
    string Description,
    string? AccentToken = null,
    string? AssetReference = null,
    int Order = 0);

/// <summary>
/// A personalisation axis of a familiar: a stable key, a human label, and the closed
/// set of values a player may pick.
/// </summary>
/// <remarks>
/// <see cref="Axis"/> is the stable identifier stored on the player profile and must
/// never be translated. <see cref="DefaultValue"/> is applied when a profile has never
/// chosen on this axis, which is what keeps a profile created before the axis existed
/// readable.
/// </remarks>
public sealed record FamiliarAxisDefinition(
    string Axis,
    string Label,
    string Description,
    string DefaultValue,
    IReadOnlyList<FamiliarOptionDefinition> Options);

/// <summary>
/// Stable axis keys. They are part of the wire contract between the configuration
/// document, the player profile and both clients; renaming one breaks stored profiles.
/// </summary>
public static class FamiliarAxes
{
    public const string Form = "form";
    public const string Tone = "tone";
    public const string WritingStyle = "writingStyle";
    public const string Accent = "accent";
    public const string Aura = "aura";
    public const string Silhouette = "silhouette";
    public const string SpeechRhythm = "speechRhythm";
    public const string LanguageRegister = "languageRegister";
    public const string InterventionDensity = "interventionDensity";

    public static IReadOnlyList<string> All { get; } =
    [
        Form, Tone, WritingStyle, Accent, Aura, Silhouette, SpeechRhythm, LanguageRegister, InterventionDensity,
    ];
}

/// <summary>
/// Builds the familiar personalisation catalogue and reconciles it with the two
/// legacy list fields.
/// </summary>
/// <remarks>
/// A configuration written before this catalogue existed carries only
/// <c>availableForms</c> and <c>availableTones</c>, and free-text <c>writingStyle</c>
/// and <c>accent</c>. <see cref="Expand"/> turns that into a full axis catalogue
/// without losing the values already in use, then mirrors the form and tone axes back
/// into the two legacy lists so a client that has not been updated keeps working.
/// </remarks>
public static class FamiliarCatalog
{
    public const int MaximumAxes = 12;
    public const int MaximumOptionsPerAxis = 24;
    public const int MaximumValueLength = 60;

    /// <summary>
    /// Returns the familiar with a complete, self-consistent axis catalogue.
    /// </summary>
    /// <remarks>
    /// When the document declares no axis the catalogue is derived from the legacy
    /// fields, augmented with the built-in axes. Values already selected by the
    /// familiar itself are always kept as options: dropping one would invalidate a
    /// profile that had legitimately chosen it.
    /// </remarks>
    public static FamiliarDefinition Expand(FamiliarDefinition familiar)
    {
        Dictionary<string, FamiliarAxisDefinition> axes = new(StringComparer.Ordinal);
        foreach (FamiliarAxisDefinition axis in familiar.Axes ?? [])
        {
            if (string.IsNullOrWhiteSpace(axis.Axis)) continue;
            axes[axis.Axis.Trim()] = axis with { Axis = axis.Axis.Trim() };
        }

        foreach (FamiliarAxisDefinition builtIn in BuildDefaultAxes(familiar))
        {
            if (!axes.TryGetValue(builtIn.Axis, out FamiliarAxisDefinition? declared))
            {
                axes[builtIn.Axis] = builtIn;
                continue;
            }

            axes[builtIn.Axis] = Reconcile(declared, builtIn, CurrentValue(familiar, builtIn.Axis));
        }

        FamiliarAxisDefinition[] ordered = FamiliarAxes.All
            .Where(axes.ContainsKey)
            .Select(key => axes[key])
            .Concat(axes.Values.Where(axis => !FamiliarAxes.All.Contains(axis.Axis, StringComparer.Ordinal)))
            .ToArray();

        return familiar with
        {
            Axes = ordered,
            // The two legacy lists stay authoritative-looking for older clients, but
            // they are now derived: the axis catalogue is the single source of truth.
            AvailableForms = Values(ordered, FamiliarAxes.Form, familiar.AvailableForms),
            AvailableTones = Values(ordered, FamiliarAxes.Tone, familiar.AvailableTones),
        };
    }

    /// <summary>
    /// Keeps a declared axis as authored, but guarantees its option set contains the
    /// value the familiar itself already uses and that its default is selectable.
    /// </summary>
    private static FamiliarAxisDefinition Reconcile(FamiliarAxisDefinition declared, FamiliarAxisDefinition builtIn, string? currentValue)
    {
        List<FamiliarOptionDefinition> options = declared.Options is { Count: > 0 }
            ? [.. declared.Options]
            : [.. builtIn.Options];

        if (!string.IsNullOrWhiteSpace(currentValue)
            && !options.Any(option => string.Equals(option.Value, currentValue, StringComparison.OrdinalIgnoreCase)))
        {
            FamiliarOptionDefinition? known = builtIn.Options
                .FirstOrDefault(option => string.Equals(option.Value, currentValue, StringComparison.OrdinalIgnoreCase));
            options.Insert(0, known ?? new FamiliarOptionDefinition(currentValue!.Trim(), currentValue.Trim(), "Valeur héritée de la configuration précédente."));
        }

        // An explicitly declared default is kept verbatim even when it is not one of the
        // options: silently rewriting it would hide the authoring mistake, so validation
        // rejects it instead. Only a blank default is filled in.
        string defaultValue = !string.IsNullOrWhiteSpace(declared.DefaultValue)
            ? declared.DefaultValue.Trim()
            : currentValue is not null && options.Any(option => string.Equals(option.Value, currentValue, StringComparison.OrdinalIgnoreCase))
                ? currentValue.Trim()
                : options[0].Value;

        return declared with
        {
            Label = string.IsNullOrWhiteSpace(declared.Label) ? builtIn.Label : declared.Label,
            Description = string.IsNullOrWhiteSpace(declared.Description) ? builtIn.Description : declared.Description,
            DefaultValue = defaultValue,
            Options = options,
        };
    }

    private static string? CurrentValue(FamiliarDefinition familiar, string axis) => axis switch
    {
        FamiliarAxes.Form => Blank(familiar.Form),
        FamiliarAxes.Tone => Blank(familiar.Tone),
        FamiliarAxes.WritingStyle => Blank(familiar.WritingStyle),
        FamiliarAxes.Accent => Blank(familiar.Accent),
        _ => null,
    };

    private static string? Blank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static IReadOnlyList<string> Values(IReadOnlyList<FamiliarAxisDefinition> axes, string axis, IReadOnlyList<string>? fallback)
    {
        FamiliarAxisDefinition? match = axes.FirstOrDefault(item => string.Equals(item.Axis, axis, StringComparison.Ordinal));
        return match is null ? fallback ?? [] : match.Options.Select(static option => option.Value).ToArray();
    }

    /// <summary>
    /// The built-in axis catalogue. Forms and tones seed themselves from the legacy
    /// lists when present, so an existing document never loses a value it published.
    /// </summary>
    private static IReadOnlyList<FamiliarAxisDefinition> BuildDefaultAxes(FamiliarDefinition familiar)
    {
        FamiliarOptionDefinition[] forms = familiar.AvailableForms is { Count: > 0 }
            ? familiar.AvailableForms.Select((value, index) => DescribeForm(value, index)).ToArray()
            : DefaultForms;
        FamiliarOptionDefinition[] tones = familiar.AvailableTones is { Count: > 0 }
            ? familiar.AvailableTones.Select((value, index) => DescribeTone(value, index)).ToArray()
            : DefaultTones;

        return
        [
            new FamiliarAxisDefinition(FamiliarAxes.Form, "Forme", "La silhouette générale de la présence qui vous accompagne.", forms[0].Value, forms),
            new FamiliarAxisDefinition(FamiliarAxes.Tone, "Ton", "L'humeur générale de ses interventions.", tones[0].Value, tones),
            new FamiliarAxisDefinition(FamiliarAxes.WritingStyle, "Style d'écriture", "La façon dont il formule ce qu'il vous dit.", DefaultWritingStyles[0].Value, DefaultWritingStyles),
            new FamiliarAxisDefinition(FamiliarAxes.Accent, "Couleur", "La teinte dominante de son apparence et de ses bulles.", DefaultAccents[0].Value, DefaultAccents),
            new FamiliarAxisDefinition(FamiliarAxes.Aura, "Aura", "Le halo qui l'entoure lorsqu'il intervient.", DefaultAuras[0].Value, DefaultAuras),
            new FamiliarAxisDefinition(FamiliarAxes.Silhouette, "Silhouette", "Sa densité visuelle à l'écran.", DefaultSilhouettes[0].Value, DefaultSilhouettes),
            new FamiliarAxisDefinition(FamiliarAxes.SpeechRhythm, "Rythme d'élocution", "La vitesse à laquelle son texte se déroule.", DefaultSpeechRhythms[0].Value, DefaultSpeechRhythms),
            new FamiliarAxisDefinition(FamiliarAxes.LanguageRegister, "Registre de langage", "Le niveau de langue qu'il emploie avec vous.", DefaultLanguageRegisters[0].Value, DefaultLanguageRegisters),
            new FamiliarAxisDefinition(FamiliarAxes.InterventionDensity, "Densité d'intervention", "La place qu'il prend sans que vous le sollicitiez.", DefaultInterventionDensities[0].Value, DefaultInterventionDensities),
        ];
    }

    private static FamiliarOptionDefinition DescribeForm(string value, int index)
    {
        FamiliarOptionDefinition? known = DefaultForms.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
        return known is null
            ? new FamiliarOptionDefinition(value, value, "Forme proposée par cette instance.", Order: index + 1)
            : known with { Order = index + 1 };
    }

    private static FamiliarOptionDefinition DescribeTone(string value, int index)
    {
        FamiliarOptionDefinition? known = DefaultTones.FirstOrDefault(option => string.Equals(option.Value, value, StringComparison.OrdinalIgnoreCase));
        return known is null
            ? new FamiliarOptionDefinition(value, value, "Ton proposé par cette instance.", Order: index + 1)
            : known with { Order = index + 1 };
    }

    private static readonly FamiliarOptionDefinition[] DefaultForms =
    [
        new("spark", "Étincelle", "Un point de lumière discret, presque absent tant qu'il ne parle pas.", "amber", Order: 1),
        new("owl", "Chouette", "Une silhouette posée qui observe avant de commenter.", "sauge", Order: 2),
        new("fox", "Renard", "Une présence mobile qui attire l'œil vers ce que vous n'avez pas lu.", "cuivre", Order: 3),
        new("tuning-fork", "Diapason", "Un objet sobre qui vibre quand quelque chose sonne faux.", "encre", Order: 4),
        new("echo", "Écho", "Une onde sans corps : rien à regarder, seulement à écouter.", "azur", Order: 5),
    ];

    private static readonly FamiliarOptionDefinition[] DefaultTones =
    [
        new("Warm", "Chaleureux", "Il vous ménage et souligne ce que vous avez bien vu.", "amber", Order: 1),
        new("Playful", "Joueur", "Il taquine, y compris vos raccourcis.", "or", Order: 2),
        new("Direct", "Direct", "Il va au fait, sans préambule ni ménagement.", "cuivre", Order: 3),
        new("Mysterious", "Énigmatique", "Il suggère plus qu'il n'explique.", "encre", Order: 4),
        new("Neutral", "Neutre", "Il expose sans colorer.", "azur", Order: 5),
    ];

    private static readonly FamiliarOptionDefinition[] DefaultWritingStyles =
    [
        new("Socratic", "Socratique", "Il ne répond pas : il demande sur quoi vous vous appuyez.", "encre", Order: 1),
        new("Concise", "Concis", "Une phrase, jamais deux.", "azur", Order: 2),
        new("Narrative", "Narratif", "Il replace ce que vous vivez dans l'histoire en cours.", "sauge", Order: 3),
        new("Analytical", "Analytique", "Il décompose la situation en éléments vérifiables.", "or", Order: 4),
        new("Laconic", "Laconique", "Il se contente d'un mot quand un mot suffit.", "cuivre", Order: 5),
    ];

    private static readonly FamiliarOptionDefinition[] DefaultAccents =
    [
        new("amber", "Ambre", "Une lumière chaude, lisible sur fond sombre.", "amber", Order: 1),
        new("encre", "Encre", "Un bleu profond, presque noir.", "encre", Order: 2),
        new("azur", "Azur", "Un bleu clair, franc et froid.", "azur", Order: 3),
        new("or", "Or", "Un jaune dense réservé aux moments d'arbitrage.", "or", Order: 4),
        new("cuivre", "Cuivre", "Un orange métallique qui accroche le regard.", "cuivre", Order: 5),
        new("sauge", "Sauge", "Un vert éteint, reposant sur de longues sessions.", "sauge", Order: 6),
        new("aube", "Aube", "Un rose pâle, le plus discret du jeu.", "aube", Order: 7),
    ];

    private static readonly FamiliarOptionDefinition[] DefaultAuras =
    [
        new("none", "Aucune", "Aucun halo : la forme seule.", Order: 1),
        new("halo", "Halo", "Un cercle net autour de la forme.", Order: 2),
        new("glow", "Lueur", "Un dégradé diffus qui pulse lentement.", Order: 3),
        new("ripple", "Onde", "Une onde qui part de la forme à chaque intervention.", Order: 4),
    ];

    private static readonly FamiliarOptionDefinition[] DefaultSilhouettes =
    [
        new("compact", "Compacte", "Petite, en marge du texte.", Order: 1),
        new("standard", "Standard", "La taille par défaut, à côté de la scène.", Order: 2),
        new("expansive", "Ample", "Occupe visiblement l'écran quand il parle.", Order: 3),
        new("minimal", "Minimale", "Réduite à un point ; seul le texte reste.", Order: 4),
    ];

    private static readonly FamiliarOptionDefinition[] DefaultSpeechRhythms =
    [
        new("instant", "Immédiat", "Le texte apparaît d'un bloc.", Order: 1),
        new("measured", "Mesuré", "Le texte se déroule à la vitesse d'une lecture calme.", Order: 2),
        new("slow", "Lent", "Le texte se déroule mot à mot.", Order: 3),
    ];

    private static readonly FamiliarOptionDefinition[] DefaultLanguageRegisters =
    [
        new("standard", "Courant", "Un français neutre et professionnel.", Order: 1),
        new("familiar", "Familier", "Il vous tutoie et raccourcit ses phrases.", Order: 2),
        new("formal", "Soutenu", "Il vous vouvoie et pèse chaque terme.", Order: 3),
        new("technical", "Technique", "Il emploie le vocabulaire du métier sans le traduire.", Order: 4),
    ];

    private static readonly FamiliarOptionDefinition[] DefaultInterventionDensities =
    [
        new("silent", "Silencieux", "Il n'intervient jamais sans être appelé.", Order: 1),
        new("sparse", "Rare", "Il intervient aux moments décisifs seulement.", Order: 2),
        new("regular", "Régulier", "Il commente chaque scène une fois.", Order: 3),
        new("constant", "Constant", "Il réagit à presque chacun de vos gestes.", Order: 4),
    ];
}