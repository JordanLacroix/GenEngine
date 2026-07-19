# Contrats HTTP

## Configuration

- `GET /experience/{frontId}` retourne la dernière configuration publiée sans référence de secret.
- `GET /admin/configuration/{frontId}` exige `config.read`.
- `PUT /admin/configuration/{frontId}` exige `config.write` et un `expectedRevision` pour une mise à jour.
- `POST /admin/configuration/{frontId}/publish` exige `config.publish` et publie une nouvelle version immuable.
- `GET /admin/configuration/field-descriptors` exige `config.read` et retourne l'aide intégrée de **chaque champ** du document de configuration. La route ne dépend d'aucun front : elle décrit le schéma, pas une instance.
- `GET /asset-packs` — packs d'assets livrés par l'instance : `packId`, `packVersion`, `configurationKey`, `description`, `assetCount`, `filesBaseUrl`.
- `GET /asset-packs/{packId}` — manifeste complet d'un pack. Un pack inconnu renvoie `asset_pack_not_found` en 404, jamais un manifeste vide.
- `GET /asset-packs/{packId}/files/{chemin}` — octets d'un asset, en `image/svg+xml`, `image/png` ou `audio/ogg`, avec `Cache-Control: public, max-age=31536000, immutable` et `X-Content-Type-Options: nosniff`.

Ces trois routes sont anonymes, comme `GET /experience/{frontId}` : un visiteur de la démonstration doit pouvoir charger un visuel ou un son avant de détenir le moindre jeton, et le contenu livré est du CC0 public. Les packs sont **en lecture seule** : ils sont versionnés avec le dépôt et copiés dans l'image, jamais téléversés à l'exécution, ce qui préserve le système de fichiers en lecture seule et l'utilisateur non-root du conteneur. `path` est réécrit en chemin de requête absolu servi par ce service ; un client n'a donc jamais à connaître l'arborescence du dépôt, et `packId` reste la clé stable même quand le dossier porte un autre nom (`assets/diapason` livre `diapason-core`).

La vue contient le jeu global, son histoire, les catégories, la politique d'authentification, les providers IA, les familiers, l'économie, l'introduction, le shell joueur, la démo, l'aide, l'onboarding, la politique assistant, le journal, les médias et les modules avec leurs permissions nécessaires.

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

- `GET /me/experience?frontId={frontId}` — familier, portefeuille, possessions et journal récent
- `GET /me/experience/bootstrap?frontId={frontId}` — prochaine action autoritative, configuration du tutoriel et état joueur
- `PUT /me/experience/familiar?frontId={frontId}` — personnalisation contrôlée **axe par axe** par le catalogue publié. Le corps accepte toujours `form`, `tone`, `writingStyle` et `accent`, et accepte en plus une carte `axes` (clé d'axe → valeur), qui l'emporte pour les clés qu'elle porte. Une valeur hors catalogue renvoie `invalid_familiar_configuration`, un axe non déclaré renvoie `unknown_familiar_axis`, un axe non renseigné retombe sur le `defaultValue` de l'axe. `customName` reste libre mais borné à 80 caractères imprimables et refuse `<`, `>`, `&` et les caractères de contrôle (`invalid_custom_name`)
- `POST /me/experience/onboarding/steps/{stepId}/complete?frontId={frontId}` — progression idempotente d'une étape
- `POST /me/experience/onboarding/skip?frontId={frontId}` — passage idempotent si autorisé
- `POST /me/experience/onboarding/reset?frontId={frontId}` — recommence le tutoriel courant
- `GET /me/experience/journal?frontId={frontId}` — journal filtrable et agrégats personnels
- `POST /me/experience/assistant/contextual-help?frontId={frontId}` — aide déterministe hors ligne et avertissement de chemin connu
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
