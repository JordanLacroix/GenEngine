# Contrats HTTP

## Configuration

- `GET /experience/{frontId}` retourne la dernière configuration publiée sans référence de secret.
- `GET /admin/configuration/{frontId}` exige `config.read`.
- `PUT /admin/configuration/{frontId}` exige `config.write` et un `expectedRevision` pour une mise à jour.
- `POST /admin/configuration/{frontId}/publish` exige `config.publish` et publie une nouvelle version immuable.
- `GET /asset-packs` — packs d'assets livrés par l'instance : `packId`, `packVersion`, `configurationKey`, `description`, `assetCount`, `filesBaseUrl`.
- `GET /asset-packs/{packId}` — manifeste complet d'un pack. Un pack inconnu renvoie `asset_pack_not_found` en 404, jamais un manifeste vide.
- `GET /asset-packs/{packId}/files/{chemin}` — octets d'un asset, en `image/svg+xml`, `image/png` ou `audio/ogg`, avec `Cache-Control: public, max-age=31536000, immutable` et `X-Content-Type-Options: nosniff`.

Ces trois routes sont anonymes, comme `GET /experience/{frontId}` : un visiteur de la démonstration doit pouvoir charger un visuel ou un son avant de détenir le moindre jeton, et le contenu livré est du CC0 public. Les packs sont **en lecture seule** : ils sont versionnés avec le dépôt et copiés dans l'image, jamais téléversés à l'exécution, ce qui préserve le système de fichiers en lecture seule et l'utilisateur non-root du conteneur. `path` est réécrit en chemin de requête absolu servi par ce service ; un client n'a donc jamais à connaître l'arborescence du dépôt, et `packId` reste la clé stable même quand le dossier porte un autre nom (`assets/diapason` livre `diapason-core`).

La vue contient le jeu global, son histoire, les catégories, la politique d'authentification, les providers IA, les familiers, l'économie, l'introduction, le shell joueur, la démo, l'aide, l'onboarding, la politique assistant, le journal, les médias et les modules avec leurs permissions nécessaires.

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

Le client iOS (dépôt `GenEngine.IOS`) consomme les quatre routes ci-dessus ainsi que `journal?limit=100` : il doit être mis à jour dans un lot dédié, ce dépôt ne le contenant pas.

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

- `GET /me/experience?frontId={frontId}` — familier, portefeuille, possessions et journal récent
- `GET /me/experience/bootstrap?frontId={frontId}` — prochaine action autoritative, configuration du tutoriel et état joueur
- `PUT /me/experience/familiar?frontId={frontId}` — personnalisation contrôlée par le catalogue publié
- `POST /me/experience/onboarding/steps/{stepId}/complete?frontId={frontId}` — progression idempotente d'une étape
- `POST /me/experience/onboarding/skip?frontId={frontId}` — passage idempotent si autorisé
- `POST /me/experience/onboarding/reset?frontId={frontId}` — recommence le tutoriel courant
- `GET /me/experience/journal?frontId={frontId}&page=1&pageSize=25&type={type}&journeyId={id}&categoryId={id}&scenarioId={id}` — journal filtrable et agrégats personnels. Filtres, pagination, `total` et `totalsByType` sont évalués en base : un joueur ayant traversé des centaines de scénarios ne charge jamais son historique complet
- `POST /me/experience/assistant/contextual-help?frontId={frontId}` — aide déterministe hors ligne et avertissement de chemin connu
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
