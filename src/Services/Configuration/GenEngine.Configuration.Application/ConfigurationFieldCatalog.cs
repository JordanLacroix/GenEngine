using System.Collections;
using System.Reflection;

namespace GenEngine.Configuration.Application;

/// <summary>
/// Integrated help for a single configuration field.
/// </summary>
/// <param name="Path">
/// Stable dotted path of the field in the published document, using the JSON member
/// names and <c>[]</c> for a collection element — <c>game.name</c>,
/// <c>economy.offers[].price</c>. This is the granularity the clients bind to: it is
/// the most direct addressing scheme, it survives a field moving inside its block,
/// and it lets a form field look its own help up without a second mapping table.
/// </param>
/// <param name="Label">Short human name of the field, shown as the form label.</param>
/// <param name="Description">What the field actually does, in one or two sentences.</param>
/// <param name="Example">A concrete admissible value, shown as a placeholder.</param>
/// <param name="Constraint">The readable rule the server enforces, when there is one.</param>
public sealed record ConfigurationFieldDescriptor(
    string Path,
    string Label,
    string Description,
    string Example,
    string? Constraint = null);

/// <summary>
/// The catalogue of per-field help served to both clients, so the same sentence is
/// written once here instead of twice in two front-ends.
/// </summary>
/// <remarks>
/// A forgotten descriptor is detectable rather than invisible: <see cref="EnumerateDocumentPaths"/>
/// walks the document type by reflection and a test compares its output to
/// <see cref="Descriptors"/>. Adding a field to <see cref="ExperienceDocument"/> without
/// describing it therefore fails the build rather than shipping an unlabelled input.
/// </remarks>
public static class ConfigurationFieldCatalog
{
    /// <summary>Every described field, ordered by path.</summary>
    public static IReadOnlyList<ConfigurationFieldDescriptor> Descriptors { get; } = Build()
        .OrderBy(static descriptor => descriptor.Path, StringComparer.Ordinal)
        .ToArray();

    private static readonly Dictionary<string, ConfigurationFieldDescriptor> ByPath =
        Descriptors.ToDictionary(static descriptor => descriptor.Path, StringComparer.Ordinal);

    public static ConfigurationFieldDescriptor? Find(string path) =>
        ByPath.TryGetValue(path, out ConfigurationFieldDescriptor? descriptor) ? descriptor : null;

    /// <summary>
    /// Walks <see cref="ExperienceDocument"/> and yields the dotted path of every leaf
    /// an administrator can edit. A leaf is a scalar, an enum, a dictionary — whose keys
    /// are data, not schema — or a collection of scalars.
    /// </summary>
    public static IReadOnlyList<string> EnumerateDocumentPaths()
    {
        List<string> paths = [];
        Walk(typeof(ExperienceDocument), string.Empty, [], paths);
        return paths.Distinct(StringComparer.Ordinal).OrderBy(static path => path, StringComparer.Ordinal).ToArray();
    }

    private static void Walk(Type type, string prefix, HashSet<Type> visiting, List<string> paths)
    {
        if (!visiting.Add(type)) return;
        foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            if (property.GetIndexParameters().Length > 0) continue;
            string path = prefix.Length == 0 ? Camel(property.Name) : $"{prefix}.{Camel(property.Name)}";
            Type propertyType = Nullable.GetUnderlyingType(property.PropertyType) ?? property.PropertyType;

            if (IsLeaf(propertyType))
            {
                paths.Add(path);
                continue;
            }

            if (ElementType(propertyType) is Type element)
            {
                if (IsLeaf(element)) paths.Add($"{path}[]");
                else Walk(element, $"{path}[]", visiting, paths);
                continue;
            }

            Walk(propertyType, path, visiting, paths);
        }

        _ = visiting.Remove(type);
    }

    private static bool IsLeaf(Type type) =>
        type.IsEnum
        || type == typeof(string)
        || type == typeof(Guid)
        || type == typeof(int)
        || type == typeof(bool)
        || type == typeof(DateTimeOffset)
        || (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IReadOnlyDictionary<,>));

    private static Type? ElementType(Type type)
    {
        if (type == typeof(string) || !typeof(IEnumerable).IsAssignableFrom(type)) return null;
        return type.IsGenericType ? type.GetGenericArguments()[0] : null;
    }

    private static string Camel(string name) =>
        name.Length == 0 ? name : char.ToLowerInvariant(name[0]) + name[1..];

    private static ConfigurationFieldDescriptor[] Build() =>
    [
        new("frontId", "Identifiant de front", "La clé technique de cette instance de jeu. Elle apparaît dans toutes les URL d'API et ne se renomme pas après la première publication.", "default", "Doit être identique à celui de la route appelée."),
        new("organizationType", "Type d'organisation", "Choisit les presets de terminologie, de modules et de rôles. Il ne change aucun contrat ni aucune règle d'autorisation.", "School", "School, Company, TrainingProvider, Community ou Custom."),

        new("organization.name", "Nom de l'organisation", "Le nom affiché de la structure qui héberge le jeu.", "Lycée Camille-Claudel", "Obligatoire."),
        new("organization.description", "Description de l'organisation", "Une phrase de contexte affichée dans l'administration.", "Structure configurable du jeu.", null),
        new("organization.units[].id", "Identifiant d'unité", "La clé stable d'une unité. Les affectations la référencent, donc elle ne se réattribue pas.", "efc447ef-fdd6-42e6-b3d8-5de6841d9bce", "Unique dans le document."),
        new("organization.units[].parentId", "Unité parente", "L'unité qui contient celle-ci. Vide pour une racine.", "(vide)", "Doit exister et ne peut pas créer de cycle."),
        new("organization.units[].type", "Type d'unité", "La nature de l'unité dans votre vocabulaire : établissement, promotion, classe, équipe.", "Class", "Obligatoire."),
        new("organization.units[].name", "Nom de l'unité", "Le nom affiché de l'unité.", "6e A", "Obligatoire."),
        new("organization.units[].code", "Code d'unité", "Un code court utilisé dans les imports de masse et les exports.", "6A", null),
        new("organization.units[].description", "Description de l'unité", "Un rappel de ce que regroupe cette unité.", "Groupe du mardi après-midi.", null),
        new("organization.units[].order", "Ordre d'affichage", "Position de l'unité dans les listes, du plus petit au plus grand.", "1", null),
        new("organization.units[].enabled", "Unité active", "Une unité désactivée reste référencée mais n'accepte plus de nouvelle affectation.", "true", null),

        new("game.name", "Nom du jeu", "Le titre affiché partout dans les clients et dans l'introduction.", "Le Diapason", "Obligatoire."),
        new("game.description", "Description du jeu", "Le résumé court montré avant la connexion.", "Dix situations professionnelles où il faut décider sans pouvoir tout vérifier.", null),
        new("game.globalStory", "Histoire globale", "Le monde commun à tous les scénarios. Il est injecté dans la génération de scénario, donc il oriente ce que le Studio produit.", "Les systèmes d'intelligence artificielle décident partout, vite, et de façon plausible.", "Obligatoire."),
        new("game.locale", "Langue", "La locale par défaut des libellés et des formats de date.", "fr-FR", null),
        new("game.timeZone", "Fuseau horaire", "Le fuseau utilisé pour les fenêtres d'affectation et les échéances.", "Europe/Paris", null),

        new("language.labels", "Dictionnaire de libellés", "Le vocabulaire du jeu, clé par clé. Toute clé absente retombe sur le libellé livré, donc vous ne redéfinissez que ce que vous voulez changer.", "nav.studio → Forge des récits", "1 à 250 entrées ; clé ≤ 80 caractères, valeur ≤ 500."),

        new("authentication.mode", "Mode d'authentification", "Détermine les moyens de connexion proposés : compte local seul, Entra ID seul, ou les deux.", "LocalOnly", "LocalOnly, EntraOnly ou Cumulative."),
        new("authentication.localEnabled", "Comptes locaux", "Autorise l'inscription et la connexion par identifiant et mot de passe.", "true", "Au moins un fournisseur doit rester actif."),
        new("authentication.entraEnabled", "Entra ID", "Autorise la connexion par Microsoft Entra ID.", "false", "Exige un tenant et un client si actif."),
        new("authentication.entraTenantId", "Tenant Entra", "L'identifiant du tenant Microsoft autorisé à se connecter.", "00000000-0000-0000-0000-000000000000", "Obligatoire si Entra ID est actif."),
        new("authentication.entraClientId", "Client Entra", "L'identifiant d'application déclaré dans le tenant.", "00000000-0000-0000-0000-000000000000", "Obligatoire si Entra ID est actif."),

        new("aiProviders[].id", "Identifiant du provider", "La clé stable du provider, référencée par les profils d'usage.", "3e6f6554-9b55-49ca-bd24-19e2f57e672a", "Unique dans le document."),
        new("aiProviders[].name", "Nom du provider", "Le nom affiché dans le Studio au moment de choisir un moteur.", "Azure AI Foundry", null),
        new("aiProviders[].type", "Type de provider", "L'adaptateur utilisé. Offline est déterministe et ne sort jamais de l'instance.", "Offline", "Offline ou AzureAiFoundry."),
        new("aiProviders[].enabled", "Provider actif", "Un provider inactif reste configuré mais n'est jamais sélectionné.", "true", null),
        new("aiProviders[].endpoint", "Endpoint", "L'URL de base du service. Vide pour le provider hors ligne.", "https://resource.openai.azure.com/openai/v1/", null),
        new("aiProviders[].deployment", "Déploiement", "Le nom du modèle ou du déploiement appelé.", "gpt-4.1-mini", null),
        new("aiProviders[].authentication", "Mode d'authentification du provider", "Comment l'instance s'authentifie auprès du provider.", "EntraId", "None ou EntraId."),
        new("aiProviders[].secretReference", "Référence de secret", "Le nom opaque du secret résolu par l'infrastructure. La valeur du secret n'est jamais stockée ici et n'est jamais republiée.", "azure-foundry-credential", "Retirée de la configuration publiée."),
        new("aiProviders[].capabilities[]", "Capacités du provider", "Les usages que ce provider accepte de servir.", "chat, scenario-generation", null),

        new("categories[].id", "Identifiant de catégorie", "La clé stable de la catégorie, référencée par les parcours, les affectations et la fin de jeu.", "2b0f1f8c-6f2f-4a1e-9c4a-0f3a1d5b7e01", "Unique dans le document."),
        new("categories[].name", "Nom de la catégorie", "Le nom affiché sur la carte du joueur.", "Lucidité", null),
        new("categories[].description", "Description de la catégorie", "Ce que la catégorie travaille, en une phrase.", "Voir ce qui est réellement là avant d'interpréter.", null),
        new("categories[].accent", "Couleur d'accent", "Le jeton de couleur utilisé pour la carte et les badges de cette catégorie.", "encre", null),
        new("categories[].order", "Ordre d'affichage", "Position de la catégorie sur la carte.", "1", null),
        new("categories[].isVisible", "Catégorie visible", "Une catégorie masquée reste jouable par lien direct mais n'apparaît plus sur la carte.", "true", null),
        new("categories[].imageUrl", "Illustration de la catégorie", "L'image de couverture affichée sur la carte.", "https://exemple.org/lucidite.avif", "URL HTTPS absolue ou référence de pack packId:assetId."),
        new("categories[].tags[]", "Étiquettes", "Des mots-clés libres utilisés pour filtrer le catalogue.", "provenance, preuve", null),
        new("categories[].scenarioIds[]", "Scénarios rattachés", "Les scénarios publiés qui composent la catégorie. C'est cette liste qui permet de dire qu'une catégorie est terminée.", "(identifiants de scénario)", null),

        new("familiars[].id", "Identifiant du familier", "La clé stable du familier, mémorisée dans le profil du joueur.", "04b758d1-862d-4f01-b2c9-d7f5ccf33a0f", "Unique dans le document."),
        new("familiars[].name", "Nom du familier", "Le nom proposé par défaut, que le joueur peut remplacer.", "Tierce", "Obligatoire."),
        new("familiars[].description", "Description du familier", "Ce que ce compagnon fait et ne fait pas, présenté au joueur avant qu'il choisisse.", "Une voix qui ne répond jamais à votre place.", null),
        new("familiars[].form", "Forme par défaut", "La forme retenue tant que le joueur n'a pas choisi la sienne.", "tuning-fork", "Doit figurer dans l'axe « form »."),
        new("familiars[].writingStyle", "Style d'écriture par défaut", "Le style retenu tant que le joueur n'a pas choisi le sien.", "Socratic", "Doit figurer dans l'axe « writingStyle »."),
        new("familiars[].tone", "Ton par défaut", "Le ton retenu tant que le joueur n'a pas choisi le sien.", "Warm", "Doit figurer dans l'axe « tone »."),
        new("familiars[].accent", "Couleur par défaut", "Le jeton de couleur retenu tant que le joueur n'a pas choisi le sien.", "amber", "Doit figurer dans l'axe « accent »."),
        new("familiars[].helpLevel", "Niveau d'aide", "À quel point le familier en dit long quand on le sollicite.", "2", "Entre 0 et 5."),
        new("familiars[].capabilities[]", "Capacités du familier", "Les services que ce familier rend hors ligne.", "hint, recap, rephrase", null),
        new("familiars[].availableForms[]", "Formes proposées (hérité)", "L'ancienne liste de formes. Elle est désormais déduite de l'axe « form » et conservée pour les clients non encore mis à jour.", "spark, owl, fox", "Dérivée : éditez l'axe « form »."),
        new("familiars[].availableTones[]", "Tons proposés (hérité)", "L'ancienne liste de tons. Elle est désormais déduite de l'axe « tone » et conservée pour les clients non encore mis à jour.", "Warm, Direct", "Dérivée : éditez l'axe « tone »."),
        new("familiars[].portraitUrl", "Portrait", "L'illustration pleine taille du familier.", "https://exemple.org/tierce.avif", "URL HTTPS absolue ou référence de pack."),
        new("familiars[].avatarUrl", "Avatar", "La vignette utilisée à côté de ses répliques.", "diapason-core:ui.familiar", "URL HTTPS absolue ou référence de pack."),
        new("familiars[].backgroundUrl", "Fond", "L'arrière-plan de l'écran de configuration du familier.", "https://exemple.org/fond.avif", "URL HTTPS absolue ou référence de pack."),
        new("familiars[].license", "Licence des visuels", "La licence sous laquelle les visuels du familier sont utilisés.", "CC0 1.0", null),
        new("familiars[].attribution", "Attribution", "L'auteur à créditer pour ces visuels.", "Kenney", null),
        new("familiars[].axes[].axis", "Clé de l'axe", "L'identifiant technique de l'axe de personnalisation. Il est mémorisé dans le profil joueur, donc il ne se renomme pas.", "speechRhythm", "Unique par familier ; jamais traduit."),
        new("familiars[].axes[].label", "Libellé de l'axe", "Le nom affiché de l'axe dans le configurateur du joueur.", "Rythme d'élocution", "Obligatoire."),
        new("familiars[].axes[].description", "Description de l'axe", "Ce que cet axe change concrètement.", "La vitesse à laquelle son texte se déroule.", null),
        new("familiars[].axes[].defaultValue", "Valeur par défaut de l'axe", "La valeur appliquée à un joueur qui n'a jamais choisi sur cet axe. C'est ce qui rend un profil antérieur à l'axe toujours lisible.", "measured", "Doit être l'une des options de l'axe."),
        new("familiars[].axes[].options[].value", "Valeur de l'option", "Le jeton stable enregistré dans le profil.", "measured", "Unique dans l'axe, ≤ 60 caractères, sans caractère de contrôle."),
        new("familiars[].axes[].options[].label", "Libellé de l'option", "Le nom affiché de l'option.", "Mesuré", "Obligatoire."),
        new("familiars[].axes[].options[].description", "Effet de l'option", "Ce que cette option change, pour que le joueur puisse choisir sans essayer.", "Le texte se déroule à la vitesse d'une lecture calme.", null),
        new("familiars[].axes[].options[].accentToken", "Jeton de couleur", "La couleur que le client utilise pour prévisualiser l'option.", "amber", null),
        new("familiars[].axes[].options[].assetReference", "Référence d'asset", "Un visuel ou un son illustrant l'option dans l'aperçu.", "diapason-core:ui.spark", "URL HTTPS absolue ou référence de pack."),
        new("familiars[].axes[].options[].order", "Ordre de l'option", "Position de l'option dans la liste proposée au joueur.", "1", null),

        new("economy.currencyCode", "Code de la monnaie", "Le code technique de la monnaie, utilisé dans les règles de récompense et les journaux.", "ACCORD", null),
        new("economy.currencyName", "Nom de la monnaie", "Le nom affiché au joueur.", "Accords", null),
        new("economy.currencyIcon", "Icône de la monnaie", "Le caractère ou l'emoji affiché à côté d'un solde.", "♪", null),
        new("economy.initialBalance", "Solde initial", "Ce que reçoit un joueur à la création de son profil.", "0", "Positif ou nul."),
        new("economy.rewardRules[].trigger", "Déclencheur de récompense", "L'événement narratif qui crédite le joueur.", "ScenarioCompleted", null),
        new("economy.rewardRules[].referenceId", "Référence du déclencheur", "L'objet visé par la règle. L'astérisque vaut pour tous.", "*", null),
        new("economy.rewardRules[].amount", "Montant crédité", "Ce que la règle crédite à chaque déclenchement idempotent.", "25", "Strictement positif."),
        new("economy.rewardRules[].description", "Motif affiché", "Le libellé écrit dans le portefeuille du joueur.", "Terminer un scénario", null),
        new("economy.offers[].id", "Identifiant de l'offre", "La clé stable de l'offre, mémorisée dans les possessions du joueur.", "370b6f82-a264-45cc-a0d0-2d71e58be15e", "Unique dans le document."),
        new("economy.offers[].name", "Nom de l'offre", "Le nom affiché dans le magasin.", "Sourdine de cuivre", null),
        new("economy.offers[].description", "Description de l'offre", "Ce que le joueur obtient réellement.", "Une apparence rare pour le familier.", null),
        new("economy.offers[].price", "Prix", "Le coût en monnaie narrative.", "80", "Positif ou nul."),
        new("economy.offers[].rewardType", "Type de récompense", "La nature de ce qui est débloqué.", "FamiliarCosmetic", null),
        new("economy.offers[].rewardReference", "Référence de récompense", "L'objet précis débloqué par l'achat.", "copper-mute", null),
        new("economy.offers[].enabled", "Offre active", "Une offre inactive reste possédée par ceux qui l'ont achetée mais n'est plus vendue.", "true", null),

        new("modules[].id", "Identifiant de module", "La clé technique du module activable.", "studio", null),
        new("modules[].name", "Nom du module", "Le nom affiché dans la navigation et l'administration.", "Studio", null),
        new("modules[].description", "Description du module", "Ce que le module permet de faire.", "Créer, générer et publier des scénarios.", null),
        new("modules[].enabled", "Module actif", "Un module désactivé disparaît de la navigation ; ses routes restent protégées côté serveur.", "true", null),
        new("modules[].requiredPermissions[]", "Permissions nécessaires", "Les permissions stables requises pour voir ce module.", "scenario.author", null),

        new("journeys[].id", "Identifiant de parcours", "La clé stable du parcours, référencée par les affectations et la fin de jeu.", "7d4c2a10-1b3e-4f52-8a6d-9c0e2f4a6b01", "Unique dans le document."),
        new("journeys[].name", "Nom du parcours", "Le nom affiché du parcours.", "Le premier accord", "Obligatoire."),
        new("journeys[].description", "Description du parcours", "Ce que le parcours fait travailler, en une phrase.", "Établir les faits avant de les interpréter.", null),
        new("journeys[].accent", "Couleur d'accent", "Le jeton de couleur du parcours sur la carte.", "encre", null),
        new("journeys[].imageUrl", "Illustration du parcours", "L'image de couverture du parcours.", "https://exemple.org/parcours.avif", "URL HTTPS absolue ou référence de pack."),
        new("journeys[].order", "Ordre d'affichage", "Position du parcours dans la liste.", "1", null),
        new("journeys[].isVisible", "Parcours visible", "Un parcours masqué reste affectable mais n'apparaît pas spontanément.", "true", null),
        new("journeys[].categoryIds[]", "Catégories du parcours", "Les catégories qui composent le parcours, dans l'ordre voulu.", "(identifiants de catégorie)", "Chaque catégorie doit exister."),
        new("journeys[].prerequisiteJourneyIds[]", "Parcours prérequis", "Les parcours à terminer avant d'ouvrir celui-ci.", "(identifiants de parcours)", "Doivent exister et ne peuvent pas se référencer eux-mêmes."),
        new("journeys[].tags[]", "Étiquettes", "Des mots-clés libres de filtrage.", "arbitrage, alerte", null),

        new("assignments[].id", "Identifiant d'affectation", "La clé stable de l'affectation.", "(guid)", "Unique dans le document."),
        new("assignments[].organizationUnitId", "Unité affectée", "L'unité dont les membres reçoivent ce contenu.", "(identifiant d'unité)", "Doit exister."),
        new("assignments[].contentType", "Type de contenu affecté", "La nature de ce qui est affecté.", "Journey", "Scenario, Category ou Journey."),
        new("assignments[].contentId", "Contenu affecté", "L'objet précis affecté à l'unité.", "(identifiant du contenu)", "Doit exister pour Category et Journey."),
        new("assignments[].name", "Nom de l'affectation", "Le libellé affiché aux encadrants et aux participants.", "Séquence de rentrée", null),
        new("assignments[].required", "Affectation obligatoire", "Une affectation obligatoire est mise en avant et comptée dans le suivi.", "true", null),
        new("assignments[].availableFrom", "Disponible à partir du", "La date d'ouverture du contenu. Vide signifie immédiatement.", "2026-09-01T08:00:00+02:00", "Doit précéder l'échéance."),
        new("assignments[].dueAt", "Échéance", "La date attendue de fin. Elle ne bloque rien, elle informe.", "2026-10-15T23:59:00+02:00", "Doit suivre la date d'ouverture."),

        new("intro.enabled", "Introduction active", "Affiche ou non la séquence d'introduction avant la connexion.", "true", null),
        new("intro.displayPolicy", "Politique d'affichage", "À quelle fréquence l'introduction est rejouée.", "OncePerVersion", "EveryLaunch, OncePerVersion ou FirstInstall."),
        new("intro.allowSkip", "Passage autorisé", "Autorise le joueur à sauter l'introduction.", "true", null),
        new("intro.minimumDisplaySeconds", "Durée minimale", "Le temps avant que le bouton de passage devienne actif.", "0", "Entre 0 et 60 secondes."),
        new("intro.scenes[].id", "Identifiant de scène", "La clé stable de la scène d'introduction.", "(guid)", "Unique dans l'introduction."),
        new("intro.scenes[].eyebrow", "Surtitre", "La ligne courte affichée au-dessus du titre.", "Le Diapason", null),
        new("intro.scenes[].title", "Titre de la scène", "La phrase forte de la scène.", "Une réponse fluide n'est pas une réponse vérifiée.", "Obligatoire."),
        new("intro.scenes[].body", "Texte de la scène", "Le paragraphe qui pose le contexte.", "2026. Vous êtes étudiant ingénieur en alternance.", null),
        new("intro.scenes[].imageUrl", "Image de la scène", "L'illustration plein écran de la scène.", "https://exemple.org/intro-1.avif", "URL HTTPS absolue ou référence de pack."),
        new("intro.scenes[].order", "Ordre de la scène", "Position de la scène dans la séquence.", "1", null),

        new("playerShell.navigation[].destination", "Destination", "L'écran ouvert par cet élément de navigation.", "map", null),
        new("playerShell.navigation[].labelKey", "Clé de libellé", "La clé du dictionnaire qui fournit le texte affiché.", "nav.map", null),
        new("playerShell.navigation[].icon", "Icône", "Le nom de l'icône rendue par le client.", "map", null),
        new("playerShell.navigation[].order", "Ordre", "Position de l'élément dans la barre de navigation.", "1", null),
        new("playerShell.navigation[].enabled", "Élément actif", "Un élément inactif disparaît de la navigation.", "true", null),
        new("playerShell.navigation[].requiredModule", "Module requis", "L'élément n'apparaît que si ce module est actif.", "play", null),

        new("demo.enabled", "Démonstration active", "Autorise un visiteur non connecté à jouer un scénario de démonstration.", "true", null),
        new("demo.scenarioSlug", "Scénario de démonstration", "Le slug du scénario publié joué en démonstration.", "la-note-de-service", null),
        new("demo.targetMinutes", "Durée annoncée", "La durée indicative affichée avant de commencer.", "18", null),
        new("demo.familiarId", "Familier de démonstration", "Le familier prêté au visiteur pendant la démonstration.", "04b758d1-862d-4f01-b2c9-d7f5ccf33a0f", null),
        new("demo.callToActionLabelKey", "Clé de l'appel à l'action", "La clé de libellé du bouton proposé à la fin de la démonstration.", "demo.createAccount", null),

        new("help.enabled", "Centre d'aide actif", "Affiche ou non le centre d'aide dans la navigation.", "true", null),
        new("help.articles[].id", "Identifiant d'article", "La clé stable de l'article d'aide.", "(guid)", "Unique dans le centre d'aide."),
        new("help.articles[].slug", "Slug de l'article", "L'identifiant lisible utilisé dans l'URL de l'article.", "premiers-pas", "Unique, obligatoire."),
        new("help.articles[].title", "Titre de l'article", "Le titre affiché dans la liste et en tête de page.", "Premiers pas", "Obligatoire."),
        new("help.articles[].summary", "Résumé", "La phrase affichée dans la liste des articles.", "Comprendre la carte et votre progression.", null),
        new("help.articles[].body", "Contenu", "Le corps de l'article, en texte simple.", "Votre carte rassemble les parcours accessibles.", null),
        new("help.articles[].contexts[]", "Contextes", "Les écrans depuis lesquels l'article est proposé automatiquement.", "map, onboarding", null),
        new("help.articles[].tags[]", "Étiquettes", "Des mots-clés de recherche.", "débuter, carte", null),
        new("help.articles[].order", "Ordre", "Position de l'article dans la liste.", "1", null),
        new("help.articles[].published", "Article publié", "Un article non publié reste éditable mais invisible.", "true", null),
        new("help.glossary[].term", "Terme", "Le mot défini dans le glossaire du jeu.", "Maîtrise", null),
        new("help.glossary[].definition", "Définition", "Ce que le terme signifie dans ce jeu précisément.", "La part des fins et branches que vous avez découvertes.", null),

        new("onboarding.id", "Identifiant du tutoriel", "La clé stable du tutoriel, mémorisée dans l'état joueur.", "9cccf7f7-fba6-45ff-a3be-42d8993bb8cc", null),
        new("onboarding.version", "Version du tutoriel", "Incrémentez-la pour reproposer le tutoriel après une refonte.", "1", "Supérieure ou égale à 1."),
        new("onboarding.enabled", "Tutoriel actif", "Un tutoriel inactif renvoie directement le joueur à la carte.", "true", null),
        new("onboarding.allowSkip", "Passage autorisé", "Autorise le joueur à passer le tutoriel.", "true", null),
        new("onboarding.requiredAfterUpgrade", "Obligatoire après montée de version", "Redemande le tutoriel aux joueurs existants après un changement de version.", "false", null),
        new("onboarding.steps[].id", "Identifiant d'étape", "La clé stable de l'étape, mémorisée dans la progression.", "(guid)", "Unique dans le tutoriel."),
        new("onboarding.steps[].title", "Titre de l'étape", "Le titre affiché dans la bulle du tutoriel.", "Votre carte", "Obligatoire."),
        new("onboarding.steps[].body", "Texte de l'étape", "Ce que l'étape explique.", "Explorez les catégories pour retrouver les scénarios.", null),
        new("onboarding.steps[].target", "Cible de l'étape", "L'élément d'interface mis en évidence.", "map", "Obligatoire."),
        new("onboarding.steps[].action", "Action attendue", "Ce que le joueur doit faire pour valider l'étape.", "open", null),
        new("onboarding.steps[].order", "Ordre de l'étape", "Position de l'étape dans la séquence.", "1", null),
        new("onboarding.steps[].required", "Étape obligatoire", "Seules les étapes obligatoires comptent pour marquer le tutoriel terminé.", "true", null),

        new("assistantPolicy.enabled", "Assistant actif", "Active le familier et l'aide contextuelle.", "true", null),
        new("assistantPolicy.requireFirstRunConfiguration", "Configuration à la première connexion", "Impose la personnalisation du familier avant d'ouvrir la carte.", "true", null),
        new("assistantPolicy.proactive", "Assistant proactif", "Autorise le familier à parler sans être sollicité.", "true", null),
        new("assistantPolicy.warnOnKnownPath", "Avertir sur un chemin connu", "Prévient le joueur quand il s'apprête à rejouer une branche déjà explorée.", "true", null),
        new("assistantPolicy.defaultFrequency", "Fréquence par défaut", "La fréquence d'intervention proposée avant tout choix du joueur.", "2", "Entre 0 et 5."),
        new("assistantPolicy.offlineCapabilities[]", "Capacités hors ligne", "Ce que l'assistant sait faire sans aucun provider IA.", "hint, recap, rephrase", null),

        new("journal.enabled", "Journal actif", "Active la chronologie personnelle du joueur.", "true", null),
        new("journal.allowExport", "Export autorisé", "Autorise le joueur à exporter son propre journal.", "true", null),
        new("journal.retentionDays", "Rétention", "Le nombre de jours conservés. Zéro signifie sans limite.", "0", "Positif ou nul."),
        new("journal.showStoryTimeline", "Afficher la chronologie narrative", "Montre les événements d'histoire à côté des événements de progression.", "true", null),

        new("media.enabled", "Médias actifs", "Coupe d'un seul geste tous les médias côté client.", "true", null),
        new("media.defaultMuted", "Muet par défaut", "Le son reste opt-in tant que le joueur ne l'active pas.", "true", null),
        new("media.locations[].location", "Emplacement applicatif", "L'écran auquel s'applique cette ambiance.", "map", "Nommé une seule fois, ≤ 40 caractères."),
        new("media.locations[].ambienceUrl", "Ambiance", "La nappe sonore jouée en fond sur cet écran.", "https://exemple.org/ambiance-carte.ogg", "URL HTTPS absolue ou référence de pack."),
        new("media.locations[].musicUrl", "Musique", "La musique jouée sur cet écran.", "https://exemple.org/musique-carte.ogg", "URL HTTPS absolue ou référence de pack."),
        new("media.locations[].backgroundUrl", "Fond visuel", "L'image de fond de cet écran.", "https://exemple.org/fond-carte.avif", "URL HTTPS absolue ou référence de pack."),
        new("media.locations[].backgroundDescription", "Description du fond", "L'alternative textuelle du fond, pour que l'image ne porte jamais seule une information.", "Une salle de réunion vide au petit matin.", null),
        new("media.locations[].bpm", "Tempo", "Le tempo déclaré du morceau, utilisé pour synchroniser les transitions.", "64", "Entre 40 et 200."),
        new("media.locations[].loop", "Lecture en boucle", "Rejoue le morceau tant que le joueur reste sur l'écran.", "true", null),
        new("media.gameOver.musicUrl", "Musique de fin de partie", "La musique jouée quand une partie se termine sur un échec narratif.", "https://exemple.org/game-over.ogg", "URL HTTPS absolue ou référence de pack."),
        new("media.gameOver.visualUrl", "Visuel de fin de partie", "L'image affichée à ce moment.", "https://exemple.org/game-over.avif", "URL HTTPS absolue ou référence de pack."),
        new("media.gameOver.visualDescription", "Description du visuel", "L'alternative textuelle de ce visuel.", "Une brume éteinte recouvre le chemin parcouru.", null),
        new("media.gameOver.labelKey", "Clé de libellé", "La clé du dictionnaire qui fournit le titre affiché.", "gameOver.title", null),

        new("finale.id", "Identifiant de la fin", "La clé stable du scénario de fin, mémorisée dans le profil une fois franchie.", "5f2c8b41-7d10-4a63-9e58-3c17a4b6d201", null),
        new("finale.enabled", "Fin active", "Une fin inactive n'est jamais évaluée ni déclenchée.", "true", null),
        new("finale.title", "Titre de la fin", "Le titre affiché au moment où le joueur atteint la fin.", "Ce qui reste après vous", "Obligatoire."),
        new("finale.summary", "Résumé de la fin", "La phrase affichée sous le titre.", "Vous avez traversé les six postures.", null),
        new("finale.body", "Texte de la fin", "Le texte de clôture. Rappelez-y que la partie continue : atteindre la fin ne verrouille rien.", "Vous n'avez pas gagné, et vous n'avez rien perdu non plus.", null),
        new("finale.mode", "Mode de combinaison", "Exiger toutes les conditions, ou n'en exiger qu'une seule.", "All", "All ou Any."),
        new("finale.visualUrl", "Visuel de la fin", "L'image affichée au déclenchement.", "https://exemple.org/finale.avif", "URL HTTPS absolue ou référence de pack."),
        new("finale.musicUrl", "Musique de la fin", "La musique jouée au déclenchement.", "https://exemple.org/finale.ogg", "URL HTTPS absolue ou référence de pack."),
        new("finale.labelKey", "Clé de libellé", "La clé du dictionnaire utilisée si vous préférez piloter le titre par le vocabulaire.", "finale.title", null),
        new("finale.conditions[].id", "Identifiant de condition", "La clé stable de la condition, utilisée pour afficher sa progression au joueur.", "(guid)", "Unique dans la fin."),
        new("finale.conditions[].type", "Type de condition", "Ce que la condition mesure dans la maîtrise déjà enregistrée.", "JourneyCompleted", "ScenariosCompleted, CategoryCompleted, JourneyCompleted, EndingsReached ou MasteryPercentReached."),
        new("finale.conditions[].description", "Description de la condition", "Ce que le joueur doit accomplir, écrit pour lui.", "Avoir terminé le parcours « Ce qui reste après toi ».", null),
        new("finale.conditions[].threshold", "Seuil", "Le nombre ou le pourcentage à atteindre, selon le type.", "8", "Requis pour ScenariosCompleted et MasteryPercentReached ; 1 à 100 pour un pourcentage."),
        new("finale.conditions[].categoryId", "Catégorie visée", "La catégorie à terminer entièrement.", "(identifiant de catégorie)", "Requis et existant pour CategoryCompleted."),
        new("finale.conditions[].journeyId", "Parcours visé", "Le parcours à terminer entièrement.", "(identifiant de parcours)", "Requis et existant pour JourneyCompleted."),
        new("finale.conditions[].endingIds[]", "Fins visées", "Les identifiants de fins de scénario à avoir atteintes.", "fin-rupture-silence", "Requis et non vide pour EndingsReached."),
        new("finale.conditions[].scenarioIds[]", "Scénarios visés", "Restreint le décompte à ces scénarios. Vide signifie tous les scénarios.", "(identifiants de scénario)", null),
    ];
}