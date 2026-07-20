# Installation et industrialisation

## Ce qui existe déjà

Le dépôt possède un seul mécanisme d'amorçage, et Diapason l'utilise sans en introduire un second.

`ConfigurationInfrastructureExtensions.MigrateAndSeedConfigurationDatabaseAsync` (appelé depuis `GenEngine.Configuration.Api/Program.cs`) applique les migrations puis, **si et seulement si la table des configurations est vide**, crée et publie le document produit par `ConfigurationService.CreateDefault`. C'est ce document qui porte désormais Le Diapason : nom du jeu, histoire globale, six catégories de posture, trois parcours avec leurs `PrerequisiteJourneyIds`, économie en `ACCORD`, scène d'introduction et scénario de démonstration (`la-note-de-service`).

Aucun code du moteur narratif n'a été modifié.

## Ce que ce mécanisme ne peut pas faire

Le seeder de Configuration ne peut pas créer de scénarios : un scénario est un document d'auteur qui vit dans le service Authoring, avec son cycle brouillon → validation → publication, et aucune frontière de service n'est franchissable. `CategoryDefinition.ScenarioIds` ne contient que des identifiants ; il ne peut pas fabriquer les scénarios qu'il désigne.

L'installation se fait donc en deux temps, ce qui est une conséquence de la séparation des services et non un contournement.

## Procédure d'installation

### 1. Première initialisation — automatique

Sur une instance neuve, démarrer la stack suffit :

```bash
docker compose up --build --detach --wait
curl -s http://localhost:5204/experience/default | jq '.document.game.name'
# "Le Diapason"
```

### 2. Contenu jouable — script d'installation

```bash
export GENENGINE_ADMIN_USER_NAME=... GENENGINE_ADMIN_PASSWORD=...
./scripts/install-diapason.sh
```

Le script lit `content/diapason/manifest.json`, puis pour chacun des dix scénarios : `POST /scenarios/import?slug=<slug>`, `POST /scenarios/{id}/validate` (échec si `isValid != true`), `POST /scenarios/{id}/analyze` (échec si une fin est inatteignable), `POST /scenarios/{id}/publish`. Il rattache ensuite les versions publiées à leur catégorie via `PUT /admin/configuration/{frontId}` et republie la configuration.

Il n'utilise que des API publiques, n'écrit dans aucune base et n'ajoute aucun endpoint.

### Idempotence par clé naturelle

Chaque scénario porte un `slug` dans le manifeste : c'est sa **clé naturelle** au sein de son front. Le script le transmet à l'import, et `POST /scenarios/import?slug=<slug>` **upserte par slug** — un slug déjà présent met à jour le brouillon existant au lieu d'en créer un neuf. Le domaine `Scenario` porte donc une propriété `Slug` facultative, protégée par un index unique filtré `(FrontId, Slug) WHERE Slug IS NOT NULL` (migration `AddScenarioSlug`).

Conséquence : **le script est rejouable sans dégât.** Un second passage ne crée aucun doublon ; il met à jour les dix brouillons et republie une nouvelle version de chacun. Le catalogue reste à dix scénarios, quel que soit le nombre de passages (le catalogue expose toujours la dernière version publiée). Un scénario archivé portant le slug est réactivé par l'import, ce qui rend l'amorçage rejouable après une remise à zéro logique.

Le slug est le **chemin idempotent**, pas une obligation : un import sans slug conserve le comportement historique (création d'un brouillon par GUID), et un scénario existant sans slug reste valide. Aucun appelant existant n'est cassé.

### Remise à zéro

`scripts/reset-diapason.sh --yes` remet une instance locale à un état propre : `docker compose down --volumes`, redémarrage sur des bases vides, réenregistrement et bootstrap de l'administrateur, puis `install-diapason.sh`.

Le choix d'un **effacement total des volumes** plutôt qu'une purge par API est délibéré et honnête : l'API Authoring n'offre pas de suppression dure — `DELETE /scenarios/{id}` archive seulement, et les lignes archivées (avec leurs versions et snapshots) subsistent — et elle ne franchit aucune frontière de service, donc elle ne peut pas défaire une pollution côté Configuration ou Play. Le seul état garanti propre est un volume vide. Comme l'installation est désormais idempotente, une instance *peuplée mais saine* n'a besoin d'aucun reset : il suffit de relancer `install-diapason.sh`. Le reset ne sert qu'au cas d'une instance déjà polluée par l'ancien flux non idempotent, où les brouillons orphelins doivent réellement disparaître.

### Limites connues

- **Le seeder de configuration ne rejoue pas.** Sur une instance qui possède déjà une configuration, `CreateDefault` n'est jamais rappelé : une instance antérieure à cette PR conserve son ancien document et doit être mise à jour par `PUT /admin/configuration/{frontId}`, ou repartir d'un volume vide.
- **Le script suppose bash 3.2** (macOS) : pas de tableau associatif, les accumulations passent par `jq`.

## Industrialisation par client

Chaque client reçoit sa propre instance : ses six bases PostgreSQL, sa configuration et son contenu. La séquence est identique partout et ne dépend d'aucun état partagé :

1. déployer la stack avec les secrets du client ;
2. la première initialisation installe Diapason comme configuration de référence ;
3. `install-diapason.sh` installe le contenu jouable ;
4. le client personnalise depuis l'administration — terminologie, catégories, parcours, familier, économie — sans toucher au code ;
5. le client remplace ou complète les scénarios par les siens via le studio Authoring.

Diapason sert alors de gabarit : un client qui écrit ses propres scénarios garde la structure par posture, ou la remplace entièrement puisque catégories et parcours sont des données de configuration.

Le point de personnalisation le plus fréquent est `OrganizationType` (`School`, `Company`, `TrainingProvider`, `Community`, `Custom`), qui sélectionne les presets de terminologie sans modifier les invariants ni les contrats.

## Vérification

```bash
dotnet test GenEngine.sln --no-build   # dont DiapasonContentTests : 10 scénarios validés
./scripts/install-diapason.sh          # import, validation, analyse et publication réels
./scripts/install-diapason.sh          # second passage : toujours 10 scénarios, aucun doublon
./scripts/smoke-test.sh                # parcours complet inchangé
```

Le double passage se vérifie sur le catalogue :

```bash
curl -s "http://localhost:5201/catalog?pageSize=100" | jq '.total'   # 10, puis toujours 10
```
