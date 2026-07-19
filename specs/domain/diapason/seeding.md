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

Le script lit `content/diapason/manifest.json`, puis pour chacun des dix scénarios : `POST /scenarios/import`, `POST /scenarios/{id}/validate` (échec si `isValid != true`), `POST /scenarios/{id}/analyze` (échec si une fin est inatteignable), `POST /scenarios/{id}/publish`. Il rattache ensuite les versions publiées à leur catégorie via `PUT /admin/configuration/{frontId}` et republie la configuration.

Il n'utilise que des API publiques, n'écrit dans aucune base et n'ajoute aucun endpoint.

### Limites connues

- **Le script n'est pas idempotent.** `POST /scenarios/import` crée toujours un nouveau brouillon (`AuthoringService.ImportAsync`). Le relancer duplique les dix brouillons et laisse les précédents orphelins. À exécuter une fois par instance.
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
./scripts/smoke-test.sh                # parcours complet inchangé
```
