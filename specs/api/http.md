# Contrats HTTP

## Configuration

- `GET /experience/{frontId}` — **anonyme** ; retourne la dernière configuration publiée, expurgée (voir « Surfaces de lecture » ci-dessous).
- `GET /client-bootstrap/{frontId}` — **anonyme** ; charge utile minimale pour un client qui démarre avant toute authentification.
- `GET /admin/configuration/{frontId}` exige `config.read` et reste la **seule** surface qui expose le document complet.
- `PUT /admin/configuration/{frontId}` exige `config.write` et un `expectedRevision` pour une mise à jour. Il rejette en `invalid_secret_reference` toute `aiProviders[].secretReference` non conforme à la grammaire de référence ; le message d'erreur ne réémet jamais la valeur refusée.
- `POST /admin/configuration/{frontId}/publish` exige `config.publish` et publie une nouvelle version immuable.
- `GET /admin/configuration/field-descriptors` exige `config.read` et retourne l'aide intégrée de **chaque champ** du document de configuration. La route ne dépend d'aucun front : elle décrit le schéma, pas une instance.
- `GET /admin/journeys/{frontId}` exige `journey.manage` — vue d'exploitation **en lecture seule** du catalogue de parcours, parcours masqués compris : `frontId`, `revision`, `publishedVersion` et, par parcours, ses catégories et prérequis résolus par nom ainsi que son nombre de scénarios. L'écriture reste `PUT /admin/configuration/{frontId}` : le Studio et l'Administration éditent déjà ce document par ce chemin, et un second chemin d'écriture ferait courir deux clients sur la même révision optimiste.
- `GET /asset-packs` — packs d'assets livrés par l'instance : `packId`, `packVersion`, `configurationKey`, `description`, `assetCount`, `filesBaseUrl`.
- `GET /asset-packs/{packId}` — manifeste complet d'un pack. Un pack inconnu renvoie `asset_pack_not_found` en 404, jamais un manifeste vide.
- `GET /asset-packs/{packId}/files/{chemin}` — octets d'un asset, en `image/svg+xml`, `image/png` ou `audio/ogg`, avec `Cache-Control: public, max-age=31536000, immutable` et `X-Content-Type-Options: nosniff`.

Ces trois routes sont anonymes, comme `GET /experience/{frontId}` : un visiteur de la démonstration doit pouvoir charger un visuel ou un son avant de détenir le moindre jeton, et le contenu livré est du CC0 public. Les packs sont **en lecture seule** : ils sont versionnés avec le dépôt et copiés dans l'image, jamais téléversés à l'exécution, ce qui préserve le système de fichiers en lecture seule et l'utilisateur non-root du conteneur. `path` est réécrit en chemin de requête absolu servi par ce service ; un client n'a donc jamais à connaître l'arborescence du dépôt, et `packId` reste la clé stable même quand le dossier porte un autre nom (`assets/diapason` livre `diapason-core`).

### Surfaces de lecture de la configuration

Trois surfaces distinctes, du plus restreint au plus complet. Ce tableau fait foi : tout champ ajouté au document doit y être classé.

| Champ | `GET /client-bootstrap/{frontId}` (anonyme) | `GET /experience/{frontId}` (anonyme) | `GET /admin/configuration/{frontId}` (`config.read`) |
|---|:--:|:--:|:--:|
| `frontId`, `version`, `publishedAt` | oui | oui | oui (plus `revision`) |
| `branding` (thème, palette d'accents, icônes) | oui | oui | oui |
| `applicationName`, `shortName`, `tagline` | oui | via `game` et `branding` | oui |
| `game.locale`, `game.timeZone` | oui | oui | oui |
| `language.labels` | oui | oui | oui |
| `intro` | oui | oui | oui |
| `authentication.mode` | oui | oui | oui |
| `authentication.localEnabled` / `entraEnabled` | non | oui | oui |
| `authentication.entraTenantId` / `entraClientId` | **non** | **non** | oui |
| `demo.enabled` | oui (booléen seul) | oui (bloc complet) | oui |
| `game.name`, `description`, `globalStory` | non | oui | oui |
| `categories`, `journeys`, `familiars`, `economy`, `modules`, `playerShell`, `help`, `onboarding`, `assistantPolicy`, `journal`, `media` | non | oui | oui |
| `aiProviders` : `id`, `name`, `type`, `enabled`, `deployment`, `capabilities` | non | oui | oui |
| `aiProviders.endpoint` / `authentication` / `secretReference` | **non** | **non** | oui (`secretReference` = référence opaque) |
| `organization` (nom, unités, hiérarchie) | **non** | **non** (`null`) | oui |
| `assignments` (affectations, fenêtres, échéances) | **non** | **non** (`[]`) | oui |

`GET /experience/{frontId}` est anonyme parce que les services `Play`, `PlayerExperience` et `Authoring` la consomment en interservice pour le catalogue jouable (familiers, économie, onboarding, politique assistant, catégories, parcours) et parce qu'un visiteur de la démonstration doit pouvoir la lire. Elle ne doit donc porter que du contenu affichable publiquement. Les identifiants de locataire Entra dont un client a besoin pour un démarrage OIDC restent publiés par Identity sur `GET /auth/providers`, qui en est la source unique ; les endpoints de providers IA, la structure d'organisation et les affectations n'ont aucun consommateur non authentifié et sont retirés.

La « référence opaque » que le tableau classe en administration seule obéit à la grammaire `scheme:identifier` définie par [`platform-configuration.md`](../platform-configuration.md#références-de-secrets), qui en est la source unique. Elle **désigne** un secret et ne le contient jamais : aucune surface, y compris celle d'administration, ne restitue la valeur du secret ; seul le service qui appelle le fournisseur la résout depuis son propre environnement.

`GET /client-bootstrap/{frontId}` ne porte volontairement **aucun** catalogue, aucune organisation, aucune affectation, aucun provider IA, aucune économie et aucun module : uniquement de quoi peindre le premier écran et proposer une entrée. `applicationName` retombe sur `game.name` si `branding.applicationName` est absent ; un client sans configuration lisible retombe sur « GenEngine ». `version` et `publishedAt` permettent la mise en cache ; la `revision` du brouillon n'est pas exposée car elle révèle une activité éditoriale non publiée.

### Bloc `branding`

Le bloc `branding` est **facultatif et purement additif** : une configuration antérieure sans ce bloc reste publiable et lisible à l'identique, et `branding` vaut alors `null` sur les trois surfaces.

| Champ | Contenu |
|---|---|
| `applicationName` | Nom d'application affiché (≤ 80 caractères) |
| `shortName` | Nom court (≤ 24) |
| `tagline` | Accroche (≤ 160) |
| `brandIconUrl` | Icône de marque de l'organisation |
| `clientIconUrl` | Icône du client |
| `logoUrl`, `faviconUrl` | Logo et favicon |
| `theme.colors` | Couleurs nommées ; les jetons `ink`, `surface`, `accent`, `accentAlt`, `success`, `warning`, `danger`, `muted` sont **obligatoires** dès que `theme` est présent |
| `theme.colorScheme` | `Dark`, `Light` ou `Auto` |
| `theme.cornerRadius` | Rayon de coin, 0 à 64 |
| `theme.fontFamily` | Famille typographique (≤ 120) |
| `accentPalette` | Associe les jetons d'accent nommés (`encre`, `or`, `sauge`, `azur`, `cuivre`, `aube`…) portés par `categories[].accent`, `journeys[].accent` et `familiars[].accent` à de vraies couleurs ; sans elle, ces accents ne sont pas rendables |

Une couleur est un hexadécimal strict `#RRGGBB` ou `#RRGGBBAA` — les couleurs CSS nommées, `rgb()` et l'abrégé à trois chiffres sont refusés pour que tous les clients rendent la même valeur. Les quatre icônes suivent la **même grammaire** que les familiers, les scènes d'introduction et les médias : URL absolue HTTPS ou référence de pack `packId:assetId`. Toute violation renvoie `invalid_branding`.

La vue `GET /experience/{frontId}` contient le jeu global, son histoire, les catégories, le mode et les fournisseurs d'authentification activés, les providers IA sans endpoint ni credential, les familiers, l'économie, l'introduction, le shell joueur, la démo, l'aide, l'onboarding, la politique assistant, le journal, les médias, le branding et les modules avec leurs permissions nécessaires.

Le bloc `media` porte le paramétrage sonore et visuel de l'instance : `enabled`, `defaultMuted`, une liste `locations` (`location`, `ambienceUrl`, `musicUrl`, `backgroundUrl`, `backgroundDescription`, `bpm`, `loop`) pour les emplacements applicatifs (`home`, `map`, `player`, `journal`, `familiar`, `shop`…) et un bloc `gameOver` (`musicUrl`, `visualUrl`, `visualDescription`, `labelKey`). Tous les assets sont facultatifs et doivent être soit des URL absolues en HTTPS, soit des références de pack `packId:assetId` résolues via le manifeste du pack livré (même grammaire que le moteur, pour qu'une instance sans serveur d'assets reste illustrée) ; un `bpm` déclaré reste entre 40 et 200. Un emplacement ne peut être nommé qu'une fois. Les violations renvoient `invalid_media`. Un opérateur pilote donc l'ambiance par instance via `PUT /admin/configuration/{frontId}` puis `POST /admin/configuration/{frontId}/publish`, sans mécanisme parallèle.

### Aide intégrée par champ

Chaque descripteur porte `path`, `label`, `description`, `example` et un `constraint` facultatif. La **granularité est le chemin de champ** : les noms JSON du document joints par un point, un élément de collection étant noté `[]` — `game.name`, `economy.offers[].price`, `familiars[].axes[].options[].value`. C'est l'adressage le plus direct, il survit au déplacement d'un champ dans son bloc, et un formulaire retrouve son aide sans table de correspondance supplémentaire.

Le catalogue est maintenu exhaustif par construction : `ConfigurationFieldCatalog.EnumerateDocumentPaths()` parcourt le type `ExperienceDocument` par réflexion, et `ConfigurationFieldCatalogTests` compare ce parcours au catalogue dans les deux sens. **Ajouter un champ sans l'accompagner d'un descripteur fait échouer les tests**, et un descripteur devenu orphelin est signalé de la même façon. Les deux clients consomment cette route au lieu de réécrire les textes.

### Bloc `finale`

Le bloc `finale` est facultatif et décrit un **scénario de fin global**, absent de toute version antérieure. Il porte `id`, `enabled`, `title`, `summary`, `body`, `mode` (`All` ou `Any`), `visualUrl`, `musicUrl`, `labelKey` et une liste `conditions`. Chaque condition porte `id`, `type`, `description` et seulement les opérandes que son type utilise :

| `type` | Opérandes lus | Satisfaite quand |
|---|---|---|
| `ScenariosCompleted` | `threshold`, `scenarioIds` facultatif | `threshold` scénarios distincts terminés |
| `CategoryCompleted` | `categoryId` | tous les scénarios rattachés à la catégorie sont terminés |
| `JourneyCompleted` | `journeyId` | tous les scénarios des catégories du parcours sont terminés |
| `EndingsReached` | `endingIds`, `threshold` facultatif | `threshold` des fins listées ont été atteintes |
| `MasteryPercentReached` | `threshold`, `scenarioIds` facultatif | la maîtrise moyenne atteint `threshold` pour cent |

Les violations renvoient `invalid_finale` ou `invalid_finale_condition`. Une catégorie ou un parcours sans scénario rattaché n'est **jamais** considéré comme terminé : traiter « rien à faire » comme « fait » déclencherait la fin sur une instance fraîchement amorcée.

L'évaluation est déterministe et se fait dans `PlayerExperience` à partir de `ScenarioMastery`, la maîtrise cross-session déjà enregistrée par (profil, version de scénario). **Aucun second système de suivi n'est introduit.** Atteindre la fin est un **seuil franchi et mémorisé**, jamais un état terminal : le profil reçoit `finaleId` et `finaleReachedAt`, une entrée de journal `FinaleReached` est écrite une seule fois, et **rien n'est verrouillé** — le joueur continue de jouer, de progresser et d'être récompensé exactement comme avant. Il n'existe volontairement aucun drapeau permettant de rendre la fin bloquante.

Toutes les API exposent `GET /health/live` et `GET /health/ready`. Les erreurs utilisent Problem Details. Les routes métier exigent un JWT Bearer sauf inscription, connexion, catalogue public et contrat interne explicitement protégé.

## Identity — port 5203

- `POST /auth/register`
- `POST /auth/login`
- `GET /auth/providers` — providers local/Entra effectivement disponibles
- `POST /auth/entra/exchange` — échange une identité Entra validée contre un JWT GenEngine
- `GET /me` — rôles et permissions effectives pour piloter les clients
- `GET|POST|PUT /admin/access/roles` — rôles personnalisés composés du catalogue stable
- `POST /admin/access/users/{userId}/roles` — affectation portée et éventuellement temporaire
- `POST /admin/access/bootstrap` — élévation initiale unique protégée par une clé dédiée

## Authoring — port 5201

- `GET /catalog?limit=20&categoryId={categoryId}` — dernières versions publiées, filtrables par catégorie
- `POST /scenarios/generate` — brouillon contextualisé par jeu, histoire globale, catégorie et prompt, via offline ou Azure AI Foundry
- `POST /scenarios/import` — migre le brouillon vers le schéma courant avant stockage
- `GET /scenarios/{id}`
- `PUT /scenarios/{id}/draft` — migre le brouillon vers le schéma courant avant stockage
- `POST /scenarios/{id}/validate`
- `POST /scenarios/{id}/analyze` — boucles, sorties garanties, risques d'impasse conditionnelle et fins inatteignables
- `POST /scenarios/{id}/preview` — prévisualisation depuis un nœud, un tour et un jour logique choisis avec état joueur injecté
- `POST /scenarios/{id}/publish`
- `GET /scenarios/{id}/versions`
- `GET /internal/scenario-versions/{versionId}` — clé interservice

## Play — port 5202

- `POST /sessions`
- `GET /sessions/{id}`
- `GET /sessions/{id}/current-step` — expose aussi le `media` optionnel du nœud (`visualUrl`, `visualDescription`, `soundUrl`) et le `media` optionnel de chaque choix visible (`soundUrl`, `animationCue`). Deux champs additifs décrivent une interaction facultative (schéma de scénario v4) : `isOptional` (booléen, `false` par défaut) indique que l'interaction courante peut être ignorée, et `exitChoices` (liste, vide par défaut) porte les choix de sortie du nœud à présenter **à côté** de l'interaction. `exitChoices` est toujours vide lorsque l'interaction est obligatoire, et lorsque l'interaction courante est déjà le `choiceSet` de sortie — ses choix sont alors dans `choices`, comme avant. Un choix de `exitChoices` se soumet par `POST /sessions/{id}/inputs`, y compris lorsque la session est en `AwaitingExternalInput` sur un `freeText` facultatif
- `GET /sessions/{id}/tree` — arbre complet avec état courant, visité, inexploré ou verrouillé, explication des conditions et `media` optionnel par nœud
- `GET /scenario-versions/{versionId}/tree` — topologie d’une version publiée **sans session** : `initialNodeId`, nœuds (`id`, `text`, `isEnding`, `media` optionnel) et arêtes (`sourceNodeId`, `targetNodeId`, `inputId`, `text`). Les états et explications de conditions dépendent d’un état de monde et sont donc volontairement absents ; le client colorie la carte avec la seule mémoire de progression. Mêmes affectations de contenu que le démarrage de session
- `GET /sessions/{id}/player` — synthèse de progression, collection et journal joueur déterministes
- `POST /sessions/{id}/inputs`
- `POST /sessions/{id}/continue` — progression d'une interaction de narration, commande idempotente
- `POST /sessions/{id}/answers` — soumission d'une réponse de quiz, commande idempotente
- `POST /sessions/{id}/text-inputs` — soumission idempotente d'un texte libre ; produit une analyse sans faire progresser le tour
- `POST /sessions/{id}/text-inputs/confirm` — confirme l'analyse et progresse, ou la refuse et revient à la saisie
- `POST /sessions/{id}/pause`
- `POST /sessions/{id}/resume`

## Player Experience — port 5205

- `GET /me/experience?frontId={frontId}` — familier, portefeuille, possessions et journal récent, plus `defaultJourneyId` et `effectiveJourney` (le parcours complet avec sa progression)
- `GET /me/experience/bootstrap?frontId={frontId}` — prochaine action autoritative, configuration du tutoriel et état joueur, `effectiveJourney` compris
- `GET /me/experience/journeys?frontId={frontId}` — exige `journey.read`. Parcours visibles du front avec, par parcours, son état de déblocage (`isUnlocked`, `blockedByJourneyIds`, `blockedByJourneyNames`), sa progression (`scenarioCount`, `startedCount`, `completedCount`, `progressPercent`) et la même progression par catégorie, pour que la carte affiche un indicateur par porte. La réponse porte aussi `defaultJourneyId`, `effectiveJourneyId` et la `revision` du profil à réutiliser en écriture
- `PUT /me/experience/journey?frontId={frontId}` — exige `journey.read`. Corps `{ expectedRevision, journeyId }` ; un `journeyId` nul efface le parcours par défaut. Le parcours est validé contre le document publié : inexistant ou invisible renvoie `journey_not_found`, prérequis non satisfaits renvoie `journey_locked`, et une révision périmée renvoie `revision_conflict` en 409
- `PUT /me/experience/familiar?frontId={frontId}` — personnalisation contrôlée **axe par axe** par le catalogue publié. Le corps accepte toujours `form`, `tone`, `writingStyle` et `accent`, et accepte en plus une carte `axes` (clé d'axe → valeur), qui l'emporte pour les clés qu'elle porte. Une valeur hors catalogue renvoie `invalid_familiar_configuration`, un axe non déclaré renvoie `unknown_familiar_axis`, un axe non renseigné retombe sur le `defaultValue` de l'axe. `customName` reste libre mais borné à 80 caractères imprimables et refuse `<`, `>`, `&` et les caractères de contrôle (`invalid_custom_name`)
- `POST /me/experience/onboarding/steps/{stepId}/complete?frontId={frontId}` — progression idempotente d'une étape
- `POST /me/experience/onboarding/skip?frontId={frontId}` — passage idempotent si autorisé
- `POST /me/experience/onboarding/reset?frontId={frontId}` — recommence le tutoriel courant
- `GET /me/experience/journal?frontId={frontId}` — journal filtrable et agrégats personnels
- `POST /me/experience/assistant/contextual-help?frontId={frontId}` — aide contextuelle résolue côté serveur

  Corps : `context`, `scenarioVersionId`, `nodeId`, `choiceId`, `alreadyExplored`,
  `authorHint`, `proactive`. `scenarioVersionId`, `nodeId` et `choiceId` servent à
  relire l'aide d'auteur portée par la version publiée via la route interne
  d'Authoring ; `authorHint` reste une surcharge cliente facultative.

  Réponse : `source`, `message`, `isFallback`, `familiarName`, `avatarUrl`,
  `modality`. `source` désigne la source du message **réellement retourné** —
  `KnownPathWarning`, `Ai`, `AuthorHint`, `ScenarioHelp`, `OfflineRule` ou
  `Suppressed` — et `isFallback` n'est vrai que pour `OfflineRule`, seule branche
  qui ne s'appuie sur aucun contenu. `modality` vaut `Hint`, `Objective`,
  `Consequence`, `Blocker`, `KnownPathWarning` ou `None`.

  L'appel est en lecture seule : il ne modifie aucun état de session, ne consomme
  aucun tour et n'entre dans aucun hash.
- `POST /me/experience/shop/purchases?frontId={frontId}` — achat idempotent
- `POST /internal/rewards` — applique une règle de récompense idempotente depuis un événement narratif
- `POST /internal/progress-events` — journalise une interaction et consolide la maîtrise cross-session de façon idempotente

## Organization — port 5206

- `GET|PUT /admin/organization/{frontId}` — front opérationnel, filtré par portée signée
- `GET|PUT /admin/organization/{frontId}/units[/{id}]` — unités hiérarchiques école/entreprise/formation
- `GET|PUT /admin/organization/{frontId}/periods[/{id}]` — années, semestres, campagnes ou exercices versionnés
- `GET|PUT|DELETE /admin/organization/{frontId}/memberships[/{id}]` — participants et encadrants temporisés et rattachables à une période
- `POST /admin/organization/{frontId}/memberships/import` — prévalidation ou import atomique et idempotent de 1 à 500 lignes
- `GET|PUT|DELETE /admin/organization/{frontId}/assignments[/{id}]` — scénarios, catégories ou parcours affectés avec disponibilité et échéance
- `GET /me/organization/{frontId}` — contexte effectif du joueur
- `GET /internal/access/{frontId}/users/{userId}` — résolution interservice protégée par clé ; Play l'utilise avant de créer une session

L'OpenAPI généré par chaque service reste la source de vérité exécutable.
