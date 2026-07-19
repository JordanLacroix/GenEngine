# Contrats HTTP

## Configuration

- `GET /experience/{frontId}` — **anonyme** ; retourne la dernière configuration publiée, expurgée (voir « Surfaces de lecture » ci-dessous).
- `GET /client-bootstrap/{frontId}` — **anonyme** ; charge utile minimale pour un client qui démarre avant toute authentification.
- `GET /admin/configuration/{frontId}` exige `config.read` et reste la **seule** surface qui expose le document complet.
- `PUT /admin/configuration/{frontId}` exige `config.write` et un `expectedRevision` pour une mise à jour. Il rejette en `invalid_secret_reference` toute `aiProviders[].secretReference` non conforme à la grammaire de référence ; le message d'erreur ne réémet jamais la valeur refusée.
- `POST /admin/configuration/{frontId}/publish` exige `config.publish` et publie une nouvelle version immuable.
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

Toutes les API exposent `GET /health/live` et `GET /health/ready`. Les erreurs utilisent Problem Details. Les routes métier exigent un JWT Bearer sauf inscription, connexion, catalogue public et contrat interne explicitement protégé.

## Pagination et recherche

**Une seule convention** s'applique à toutes les listes, quel que soit le service. Les conventions `offset`/`limit` qui coexistaient sur le catalogue et le journal sont supprimées : deux grammaires concurrentes obligeaient chaque client à savoir laquelle s'applique à quelle route, sans rien apporter.

Paramètres de requête :

| Paramètre | Type | Défaut | Bornes | Rôle |
|---|---|---|---|---|
| `page` | entier | `1` | ramené à `1` si `< 1` | numéro de page, **base 1** |
| `pageSize` | entier | `25` | clampé à `[1, 100]` | taille de page |
| `query` | texte | absent | — | sous-chaîne recherchée, insensible à la casse (`ILIKE %terme%`). Les accents ne sont **pas** normalisés : « eleve » ne trouve pas « élève » |

Réponse : toute liste renvoie la **même enveloppe**, jamais un tableau nu.

```json
{ "items": [], "page": 1, "pageSize": 25, "total": 0 }
```

`total` est le nombre d'éléments de l'**ensemble filtré**, pas de la page. Une `page` au-delà du dernier élément renvoie `items` vide et le `total` réel — ce n'est pas une erreur. Le journal joueur ajoute `totalsByType` à cette enveloppe ; cet agrégat porte lui aussi sur l'ensemble filtré et reste identique d'une page à l'autre.

Les filtres et les agrégats sont évalués en base : aucune surface ne matérialise une collection complète pour la découper ensuite en mémoire.

### Rupture de contrat introduite par cette convention

Quatre routes renvoyaient un **tableau nu** et renvoient désormais l'enveloppe. Un client qui désérialise une liste directement casse tant qu'il n'est pas mis à jour :

| Route | Avant | Après |
|---|---|---|
| `GET /catalog` | `[PublishedScenarioView]` | `{ items, page, pageSize, total }` |
| `GET /scenarios/{id}/versions` | `[ScenarioVersionView]` | `{ items, page, pageSize, total }` |
| `GET /admin/organization/{frontId}/units` | `[UnitView]` | `{ items, page, pageSize, total }` |
| `GET /admin/organization/{frontId}/periods` | `[PeriodView]` | `{ items, page, pageSize, total }` |

Les paramètres `offset` et `limit` de `GET /catalog` et `GET /me/experience/journal` sont remplacés par `page` et `pageSize` ; ils ne sont plus acceptés. `GET /me/experience/journal` conserve sa forme d'objet et gagne `page` et `pageSize` — l'ajout de champs est compatible.

Cela fait **cinq surfaces** au total : les quatre routes du tableau, plus `GET /me/experience/journal` dont les paramètres changent sans que sa forme de réponse change.

Les clients vivant dans des dépôts distincts, ils sont alignés en parallèle : `GenEngine.Web#24` et `GenEngine.IOS#23`. Les trois lots doivent être fusionnés ensemble — cette enveloppe est désormais un contrat partagé et sa forme ne peut plus changer sans les rouvrir. Le client Web lève délibérément une erreur nommée si le serveur renvoie un tableau nu, pour rendre le couplage visible plutôt que silencieux.

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

- `GET /catalog?page=1&pageSize=25&categoryId={categoryId}&query={texte}` — dernières versions publiées, triées par date de publication décroissante, filtrables par catégorie et par titre. Paginé : tout scénario publié est atteignable quel que soit le volume du catalogue
- `POST /scenarios/generate` — brouillon contextualisé par jeu, histoire globale, catégorie et prompt, via offline ou Azure AI Foundry
- `POST /scenarios/import` — migre le brouillon vers le schéma courant avant stockage
- `GET /scenarios/{id}`
- `PUT /scenarios/{id}/draft` — migre le brouillon vers le schéma courant avant stockage
- `POST /scenarios/{id}/validate`
- `POST /scenarios/{id}/analyze` — boucles, sorties garanties, risques d'impasse conditionnelle et fins inatteignables
- `POST /scenarios/{id}/preview` — prévisualisation depuis un nœud, un tour et un jour logique choisis avec état joueur injecté
- `POST /scenarios/{id}/publish`
- `GET /scenarios/{id}/versions?page=1&pageSize=25` — versions publiées d'un scénario, par numéro croissant
- `GET /internal/scenario-versions/{versionId}` — clé interservice

## Play — port 5202

- `POST /sessions`
- `GET /sessions/{id}`
- `GET /sessions/{id}/current-step` — expose aussi le `media` optionnel du nœud (`visualUrl`, `visualDescription`, `soundUrl`) et le `media` optionnel de chaque choix visible (`soundUrl`, `animationCue`). Deux champs additifs décrivent une interaction facultative (schéma de scénario v4) : `isOptional` (booléen, `false` par défaut) indique que l'interaction courante peut être ignorée, et `exitChoices` (liste, vide par défaut) porte les choix de sortie du nœud à présenter **à côté** de l'interaction. `exitChoices` est toujours vide lorsque l'interaction est obligatoire, et lorsque l'interaction courante est déjà le `choiceSet` de sortie — ses choix sont alors dans `choices`, comme avant. Un choix de `exitChoices` se soumet par `POST /sessions/{id}/inputs`, y compris lorsque la session est en `AwaitingExternalInput` sur un `freeText` facultatif. Un champ additif décrit un document (schéma de scénario v6) : `document` (objet, `null` par défaut) est renseigné uniquement lorsque `kind` vaut `Document`, et porte `title`, `nature`, `headers`, `excerpt` et `blocks` tels que définis dans [`../domain/scenario-schema.md`](../domain/scenario-schema.md). `isOptional` et `exitChoices` s'appliquent à un document comme à toute autre interaction, donc un document facultatif se saute par un choix de sortie
- `GET /sessions/{id}/tree` — arbre complet avec état courant, visité, inexploré ou verrouillé, explication des conditions et `media` optionnel par nœud
- `GET /scenario-versions/{versionId}/tree` — topologie d’une version publiée **sans session** : `initialNodeId`, nœuds (`id`, `text`, `isEnding`, `media` optionnel) et arêtes (`sourceNodeId`, `targetNodeId`, `inputId`, `text`). Les états et explications de conditions dépendent d’un état de monde et sont donc volontairement absents ; le client colorie la carte avec la seule mémoire de progression. Mêmes affectations de contenu que le démarrage de session
- `GET /sessions/{id}/player` — synthèse de progression, collection et journal joueur déterministes
- `POST /sessions/{id}/inputs`
- `POST /sessions/{id}/continue` — progression d'une interaction de narration, commande idempotente
- `POST /sessions/{id}/document-consultations` — consultation du document de l'étape courante, commande idempotente (`commandId`, `expectedRevision`). Applique les `consultEffects` du document, historise la consultation et avance à l'interaction suivante ; une commande rejouée est retournée telle quelle sans réappliquer les effets. Répond `interaction_not_document` lorsque l'étape courante n'est pas un document
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
- `PUT /me/experience/familiar?frontId={frontId}` — personnalisation contrôlée par le catalogue publié
- `POST /me/experience/onboarding/steps/{stepId}/complete?frontId={frontId}` — progression idempotente d'une étape
- `POST /me/experience/onboarding/skip?frontId={frontId}` — passage idempotent si autorisé
- `POST /me/experience/onboarding/reset?frontId={frontId}` — recommence le tutoriel courant
- `GET /me/experience/journal?frontId={frontId}&page=1&pageSize=25&type={type}&journeyId={id}&categoryId={id}&scenarioId={id}` — journal filtrable et agrégats personnels. Filtres, pagination, `total` et `totalsByType` sont évalués en base : un joueur ayant traversé des centaines de scénarios ne charge jamais son historique complet
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
- `GET|PUT /admin/organization/{frontId}/units[/{id}]` — unités hiérarchiques école/entreprise/formation. La liste est paginée et cherchable sur le nom et le code
- `GET|PUT /admin/organization/{frontId}/periods[/{id}]` — années, semestres, campagnes ou exercices versionnés. La liste est paginée et cherchable sur le nom et le code
- `GET|PUT|DELETE /admin/organization/{frontId}/memberships[/{id}]` — participants et encadrants temporisés et rattachables à une période. `query` porte sur le nom et le code de l'unité de rattachement, une affectation n'ayant aucun champ texte propre
- `POST /admin/organization/{frontId}/memberships/import` — prévalidation ou import atomique et idempotent de 1 à 500 lignes
- `GET|PUT|DELETE /admin/organization/{frontId}/assignments[/{id}]` — scénarios, catégories ou parcours affectés avec disponibilité et échéance
- `GET /me/organization/{frontId}` — contexte effectif du joueur
- `GET /internal/access/{frontId}/users/{userId}` — résolution interservice protégée par clé ; Play l'utilise avant de créer une session

### Pagination des unités hiérarchiques

Les unités forment un arbre, mais `GET /admin/organization/{frontId}/units` les pagine **à plat**, triées par nom, chaque élément exposant son `parentId`. Le client reconstruit l'arborescence à partir des `parentId` ; tant que toutes les pages sont parcourues, l'arbre obtenu est complet et `total` reste le nombre d'unités du front.

Conséquence assumée : un parent peut se trouver sur une page ultérieure à celle de son enfant. Un client qui affiche l'arbre doit donc rattacher les nœuds orphelins au fur et à mesure, et non supposer que le parent est déjà connu.

L'alternative — paginer par niveau ou par sous-arbre — a été écartée : elle coupe une fratrie au milieu d'une page, rend `total` ambigu (total des racines ? de l'arbre entier ?) et impose au serveur de connaître l'état de dépliage du client. La pagination à plat garde un contrat unique pour toutes les listes ; un front qui a besoin de charger une branche précise filtre déjà par `query`.

L'OpenAPI généré par chaque service reste la source de vérité exécutable.
