# Passage de relais

Dernière mise à jour : 18 juillet 2026.

## État vérifié

- `main` contient le backend jouable distribué et la PR `#40` d'exploitation multi-organisation ; la branche `codex/organization-operations` complète les périodes, imports de masse et parcours affectés.
- Les six services `Authoring`, `Play`, `Identity`, `Configuration`, `PlayerExperience` et `Organization` sont autonomes et disposent chacun de leur PostgreSQL.
- Le moteur `GenEngine.Narrative` est pur, déterministe et partagé comme bibliothèque embarquée.
- Le parcours inscription → connexion → import → validation → analyse → prévisualisation → publication → session → choix → replay passe avec `scripts/smoke-test.sh`.
- Le moteur couvre l'état joueur riche, les interactions typées, les gates de caractéristiques, le texte libre confirmé, les sauvegardes versionnées avec migrations chaînées, les effets différés conditionnels avec date logique et l'arbre de session.
- Play expose une projection joueur stable regroupant synthèse, collection et journal.
- Les médias sont paramétrables de bout en bout : le schéma de scénario v3 ajoute un `media` optionnel par nœud (visuel, description alternative, son) et par choix (son d'interaction, `animationCue`), tandis que Configuration publie les ambiances par emplacement applicatif et les médias de game over. Tout est facultatif, en HTTPS et jamais porteur exclusif d'information ; le moteur ne fait que transporter des références.
- Le schéma de scénario v4 rend une interaction facultative via `isOptional`. Par défaut — drapeau absent — tout reste obligatoire, donc les scénarios et sauvegardes existants sont inchangés. Une interaction facultative est présentée à côté des choix de sortie du nœud : la jouer applique ses effets et peut révéler un choix conditionné, l'ignorer quitte le nœud sans laisser de trace. La sortie n'est offerte que si tout ce qui reste avant le `choiceSet` terminal est facultatif, donc une interaction obligatoire continue de bloquer. Voir `specs/domain/scenario-schema.md`.
- Le moteur accepte une analyse d'entrée substituable mais validée contre la rubrique, et représente les effets externes par des événements ordonnés sans I/O.
- Authoring expose l'analyse des boucles, sorties garanties, impasses conditionnelles et fins inatteignables, ainsi que la prévisualisation depuis un état injecté.
- Les six API exportent logs structurés, traces HTTP et métriques via OpenTelemetry.
- La surcouche locale fournit Collector, Prometheus, Tempo, Loki et Grafana.
- Le dashboard Grafana `GenEngine — Vue d’ensemble` est provisionné.
- `HRD-003` livre des SLI/SLO provisoires : voir `specs/process/slo.md`, les règles Prometheus sous `deploy/observability/rules/` et le dashboard `GenEngine — SLO et budget d’erreur`.
- `HRD-004` livre l’audit métier : primitive `IAuditLog` dans `GenEngine.Observability`, événements émis à la frontière Api des trois services, politique de non-fuite dans `specs/process/audit.md`.
- `HRD-005` équipe l’appel `Play → Authoring` de résilience (timeouts, retry borné, circuit breaker) via `Microsoft.Extensions.Http.Resilience` : voir `specs/process/resilience.md`.
- `HRD-006` livre la sauvegarde et la restauration chiffrées des trois PostgreSQL : scripts `scripts/backup-databases.sh`, `scripts/restore-database.sh` et `scripts/lib/age-crypto.sh` (chiffrement `age`, dumps `pg_dump -Fc`), procédure et test dans `specs/process/backup-restore.md`. Aucun code applicatif modifié.
- La dernière PR fonctionnelle fusionnée est la PR GitHub `#40`, qui livre Organization et les affectations runtime scoped.
- Le pack d'assets `diapason-core` est livré sous `assets/diapason/` : 62 fichiers, 640 Kio, tous CC0 1.0 et tous produits par Kenney. Il contient les éléments d'interface, icônes, sons d'interface et signatures courtes, avec un manifeste `asset-manifest.json`, un fichier `LICENSES.md` traçant chaque source, et le script sans dépendance `scripts/verify-asset-manifest.py`. Voir `specs/media-assets.md`. `Configuration` publie désormais ce manifeste et sert les octets : le pack n'est plus de la donnée inerte.
- Au moment du handoff, le dépôt était propre, synchronisé avec `origin/main`, la stack complète était active et tous ses conteneurs étaient sains.

## Démarrage rapide de reprise

```bash
git status --short --branch
git pull --ff-only
dotnet restore GenEngine.sln --locked-mode
dotnet build GenEngine.sln --no-restore -warnaserror
dotnet test GenEngine.sln --no-build
docker compose -f compose.yaml -f compose.observability.yaml up --build --detach --wait
./scripts/smoke-test.sh
```

Endpoints locaux :

| Composant | Adresse | Nature |
|---|---|---|
| Authoring API | `http://localhost:5201` | API HTTP |
| Play API | `http://localhost:5202` | API HTTP |
| Identity API | `http://localhost:5203` | API HTTP |
| Configuration API | `http://localhost:5204` | API HTTP |
| Player Experience API | `http://localhost:5205` | API HTTP |
| Organization API | `http://localhost:5206` | API HTTP |
| Grafana | `http://localhost:3000` | Interface métriques, logs et traces |
| Prometheus | `http://localhost:9090` | Interface et API métriques |
| Loki | `http://localhost:3100` | API uniquement |
| Tempo | `http://localhost:3200` | API uniquement |

Pour arrêter sans supprimer les données :

```bash
docker compose -f compose.yaml -f compose.observability.yaml down
```

N’utilise `--volumes` que si la perte des données locales est explicitement souhaitée.

## État fonctionnel configurable — jalon 4

Le jalon 3 (durcissement) est **clos** : `HRD-001` à `HRD-007` sont traitées.

`HRD-007` (outbox) est résolue par une **décision documentée de ne rien ajouter** : aucun consommateur asynchrone n’existe (ni bus, ni file, ni worker), voir l’ADR [`specs/adr/0004-no-outbox-without-async-consumer.md`](adr/0004-no-outbox-without-async-consumer.md). Réévaluer uniquement quand un consommateur asynchrone réel apparaîtra.

Le control plane Configuration, les rôles custom, les permissions stables, les modes Local/Entra/cumulatif, Azure AI Foundry, les catégories, le familier personnalisable et la première économie/magasin sont livrés. Le vocabulaire et les copies du jeu sont désormais publiés dans un dictionnaire extensible et éditables depuis les deux clients ; « Mote » n’est plus un nom imposé. Authoring génère maintenant un scénario à partir du jeu global, de sa catégorie et du prompt auteur. Play relaie les événements `economy.reward` vers PlayerExperience avec une clé idempotente stable.

La hiérarchie d'organisation est désormais opérationnelle dans un service autonome : fronts, unités hiérarchiques, memberships participant/encadrant et affectations scénario/catégorie/parcours avec fenêtres et échéances. Play refuse le démarrage d'un scénario non affecté et conserve le front autoritatif du snapshot dans la session, le journal et les récompenses. Les clients Web et iOS exposent le workflow correspondant dans l'administration.

### Tranche `feat/product-operations` vérifiée le 18 juillet 2026

- Identity expose une recherche paginée des utilisateurs, leur détail et leurs affectations ; activation, désactivation et suppression logique sont protégées contre l'auto-verrouillage et la suppression du dernier détenteur actif de `rbac.manage`.
- Les rôles custom et affectations peuvent être supprimés ; les rôles système restent protégés. Le catalogue de permissions est synchronisé au démarrage.
- Configuration porte désormais parcours, relation N-N aux catégories, rattachement de scénarios, modèle d'affectation avec fenêtre/échéance et assets familiers HTTPS avec licence/attribution.
- Authoring recherche/pagine les brouillons et permet leur archivage optimiste ; le catalogue public ignore les scénarios archivés.
- Migrations EF `AddUserLifecycle` et `AddScenarioLifecycle` ajoutées.
- Validation locale : 87 tests backend réussis. Les clients Web et iOS ont aussi été construits sur leurs branches homologues.

### Tranche `feat/organization-runtime-scale` — validation en cours le 18 juillet 2026

- ADR 0006 et service `Organization` DDD/Clean avec PostgreSQL indépendant.
- CRUD audité des unités, memberships et affectations, scopes de front signés et résolution `/me`/interservice.
- contrôle allow/deny dans Play sur les affectations directes de scénario ou de catégorie ; front figé dans le snapshot et la session.
- écrans d'exploitation Web/iOS séparés du studio, avec création, listes et suppressions utiles.
- tests d'isolation croisée, validité temporelle, hiérarchie cyclique et contrôle du démarrage.

La tranche suivante complète les périodes métier nommées, l'import de masse prévalidé et idempotent, l'historique des memberships, la résolution d'une affectation de parcours complet et le catalogue filtré dans les clients. Restent l'export de masse, l'héritage multi-portée global, l'isolation systématique des services autres qu'Organization/Play, le snapshot de session de l'assistant, le metering/quota IA et l'import Codex Pets.

### Tranche `feat/diapason-reference-configuration` — vérifiée le 19 juillet 2026

- La configuration de référence **Le Diapason** remplace « Les braises sous la brume ». `ConfigurationService.CreateDefault` porte désormais six catégories de posture (Lucidité, Discernement, Arbitrage, Courage, Transmission, Autonomie), trois parcours chaînés par `PrerequisiteJourneyIds`, l'économie `ACCORD` et le scénario de démo `la-note-de-service`.
- Dix scénarios `schemaVersion` 2 sous [`content/diapason/scenarios/`](../content/diapason/scenarios/), un manifeste, et la bible d'univers sous [`specs/domain/diapason/`](domain/diapason/).
- `DiapasonContentTests` valide les dix documents via `ScenarioMigrationPipeline` + `ScenarioValidator`, vérifie l'absence d'impasse via `ScenarioAnalyzer.Explore`, et fige la règle de rotation quotidienne. 126 tests backend au vert.
- `scripts/install-diapason.sh` importe, valide, analyse et publie les dix scénarios sur une instance vivante, puis les rattache à leur catégorie. Exécuté réellement sur la stack Compose.
- Aucun code du moteur narratif modifié. `scripts/smoke-test.sh` mis à jour (`BRAISE` → `ACCORD`) et repassé.

**Manques identifiés, non traités ici** (tâches `DIA-008` à `DIA-011`) :

- le moteur ne distingue pas une fin d'échec d'une fin de réussite — `isEnding` est le seul concept terminal, et une partie perdue arrive en `Completed` exactement comme une partie gagnée. Vérifié en jouant `fin-rupture-silence` de bout en bout. Diapason contourne par convention de nommage (`fin-rupture-*`), ce qui n'est pas opposable côté service ni exploitable par `PlayerProjectionBuilder` ou l'économie ;
- la rotation quotidienne est documentée et testée mais non exposée : le modèle de configuration n'a aucun champ de mise en avant, et en ajouter un relève d'une décision produit ;
- `install-diapason.sh` n'est pas idempotent : `POST /scenarios/import` crée toujours un nouveau brouillon ;
- le seeder de configuration ne rejoue jamais sur une base non vide : une instance antérieure conserve son ancien document.

### Tranche `feat/journeys-default-progress` — vérifiée le 19 juillet 2026

- Le parcours devient un objet de premier plan côté joueur : `PlayerProfile.DefaultJourneyId` (colonne nullable, migration EF `AddDefaultJourney`), `GET /me/experience/journeys` et `PUT /me/experience/journey` sous `journey.read`, avec le contrôle optimiste `ExpectedRevision` déjà en place. Un `journeyId` nul efface le choix.
- Le choix est validé contre le document publié : `journey_not_found` pour un parcours inexistant ou invisible, `journey_locked` pour des prérequis non satisfaits. `GET /me/experience` et `GET /me/experience/bootstrap` portent `defaultJourneyId` et `effectiveJourney`, donc un client filtre sa carte sans second appel.
- Progression agrégée par parcours **et** par catégorie depuis `ScenarioMastery`, qui n'est jamais recalculée : scénarios, entamés, terminés et pourcentage moyen. `ScenarioMastery` étant clé par version, le repli est fait sur deux critères indépendants : meilleure version pour le pourcentage, OU logique sur toutes les versions pour l'achèvement.
- Un parcours prérequis **à périmètre vide compte comme terminé** et ne verrouille rien. L'inverse créait une impasse définitive et non réparable dès qu'une catégorie était supprimée après publication.
- Tous les rôles système — Player, Creator et Administrator — sont désormais synchronisés au démarrage d'Identity par une seule boucle pilotée par les mêmes constantes que l'amorçage. Le rattrapage ne couvrait que Player et Administrator, et les listes de permissions étaient dupliquées entre création et rattrapage, ce qui avait déjà fait dériver Creator.
- `journey.manage` est enfin câblée : `GET /admin/journeys/{frontId}` côté `Configuration`, en **lecture seule** et assumée comme telle — l'écriture reste `PUT /admin/configuration/{frontId}` pour ne pas faire courir le Studio et l'Administration sur deux chemins d'écriture de la même révision optimiste.
- Détection de cycle des `PrerequisiteJourneyIds` corrigée sur le modèle des unités d'organisation : `A → B → A` et `A → B → C → A` sont refusés avec `journey_cycle`, l'auto-référence reste `invalid_journey`. Le partage d'une catégorie entre parcours reste valide et est couvert par un test.
- `journey.read` ajoutée aux presets Player et Creator et synchronisée au démarrage d'Identity. Additif de bout en bout : une configuration et un profil existants fonctionnent sans parcours par défaut. 212 tests backend au vert après fusion de `main` (branding, hotlink et aide contextuelle).
- Cohabitation vérifiée avec l'aide contextuelle : la résolution d'aide ne lit jamais `ScenarioMastery` — son `AlreadyExplored` vient de la requête appelante — et n'appelle ni `Map` ni `BuildJourneys`. Les deux fonctionnalités ne partagent que `GetOrCreateAsync` sur `PlayerProfile`, antérieur aux deux.
- **Non traité** : aucun client Web ou iOS ne consomme encore ces routes ; la sélection reste invisible tant que le câblage client n'est pas fait. Les catégories du Diapason ne portent aucun `scenarioIds`, donc la progression y vaut zéro tant que `install-diapason.sh` n'a pas rattaché les scénarios.

### Tranche `feat/branding-client-bootstrap` — vérifiée le 19 juillet 2026

- Bloc `branding` **facultatif et purement additif** dans `ExperienceDocument` : nom d'application, nom court, accroche, quatre icônes, `theme` (couleurs nommées avec huit jetons obligatoires, `colorScheme`, rayon de coin, typographie) et `accentPalette`. Cette dernière associe enfin les jetons d'accent de `CategoryDefinition`, `JourneyDefinition` et `FamiliarDefinition` à de vraies couleurs. Validation `invalid_branding` : hexadécimal strict `#RRGGBB`/`#RRGGBBAA`, icônes passées par l'`IsValidAssetUrl` existant (HTTPS absolu ou `packId:assetId`). Une configuration sans `branding` reste publiable et lisible à l'identique — test dédié.
- Nouvelle route anonyme `GET /client-bootstrap/{frontId}` : identité, marque, locale, fuseau, libellés, intro, mode d'authentification seul, drapeau démo, `version`/`publishedAt`. Rien d'autre.
- **Correctif de sécurité** sur `GET /experience/{frontId}`, anonyme : la projection ne porte plus les identifiants de locataire et de client Entra, les endpoints et schémas d'authentification des providers IA, la structure d'organisation (unités et description vidées, l'objet restant présent car le client iOS le déclare non optionnel) ni les affectations (`[]`). Le document complet reste servi par `GET /admin/configuration/{frontId}` sous `config.read`, ce que couvre un test dédié. Les identifiants Entra dont un client a besoin sont déjà publiés par Identity sur `GET /auth/providers`, seule source pour un démarrage OIDC. Les trois consommateurs interservice (`Play` → `journeys`, `PlayerExperience` → `familiars`/économie/onboarding/politique assistant, `Authoring` → jeu et catégories) n'ont pas été affectés : aucune route interne supplémentaire n'a été nécessaire. Répartition champ par champ dans [`api/http.md`](api/http.md).
- Diapason porte un `branding` aligné sur la direction artistique (`specs/domain/diapason/README.md`) et le bloc `palette` du manifeste d'assets. **Toutes les icônes restent nulles** : le pack `diapason-core` ne fournit ni icône de marque, ni logo, ni favicon, et son champ `gaps` acte l'absence.
- 186 tests backend au vert. `scripts/smoke-test.sh` vérifie désormais l'absence de fuite sur `/experience` et la forme minimale de `/client-bootstrap`.

### Tranche `feat/assistant-contextual-help`

- Le schéma de scénario v5 ajoute un objet `help` facultatif sur les nœuds et les choix (`hint`, `objective`, `consequence`, `blocker`), purement de présentation. Migration chaînée `scenario-v4-to-v5`, validation conditionnée à `AuthorHelpSchema` et non à `LatestSchema`. Le hash canonique d'un snapshot v4 et son état final rejoué sont figés depuis le moteur d'avant le changement (`dbd5bd1d…c040`) et vérifiés par test.
- `PlayerExperience` résout l'aide côté serveur : `scenarioVersionId`, `nodeId` et `choiceId`, jusqu'ici morts, servent à relire la version publiée via la route interne d'Authoring, avec la même famille de résilience que `Play → Authoring`. Authoring indisponible dégrade vers les règles hors ligne.
- `source` et `isFallback` ne mentent plus : `source` désigne le message réellement retourné, `isFallback` n'est vrai que pour `OfflineRule`. La réponse porte en plus la modalité employée. Le niveau d'aide choisit la modalité, la fréquence d'intervention filtre la seule aide proactive.
- Le port `IAssistantAiClient` est en place avec repli hors ligne garanti dans le service lui-même. **Aucun fournisseur réel n'est implémenté ni validé de bout en bout** : le défaut enregistré se déclare non configuré, et le branchement n'est couvert que par doubles (succès, erreur, dépassement de délai). Câbler un fournisseur réel reste à faire.
- Un test vérifie qu'un appel d'aide n'écrit rien : ni sauvegarde, ni révision, ni journal, ni portefeuille.

### Tranche `feat/secret-resolution` — vérifiée le 19 juillet 2026

**Constat de départ, vérifié** : `AiProviderDefinition.SecretReference` existait depuis
longtemps et `ConfigurationService.GetPublishedAsync` prenait soin de l'effacer des projections
anonymes, mais **aucun chemin du dépôt ne transformait cette référence en identifiant
utilisable**. Les seules occurrences de `SecretReference` dans tout le dépôt étaient la
déclaration du record, la ligne de redaction, la valeur par défaut et l'assertion de redaction
dans les tests. La valeur par défaut était `"azure-foundry-credential"` : une chaîne opaque
sans grammaire ni résolveur.

- Nouveau building block [`GenEngine.Secrets`](../src/BuildingBlocks/GenEngine.Secrets/) — sans
  dépendance, sans I/O disque : `SecretReference` (grammaire `scheme:identifier`),
  `SecretValue` (rendus implicites rabattus sur `***`, `Reveal()` explicite),
  `SecretResolution` (échec = valeur, cause close), `ISecretResolver`/`ISecretStore`,
  `EnvironmentSecretResolver` (schéma `env`) et `SecretStore` (dispatch par schéma).
- `AiProviderCredentialResolver` dans `Configuration.Application` traduit un
  `AiProviderDefinition` en `AiProviderAvailability` (`ready`, `provider_disabled`,
  `no_credential_required`, `secret_not_found`…) ou en `SecretValue?`.
- Grammaire refusée à l'écriture : `PUT /admin/configuration/{frontId}` renvoie
  `invalid_secret_reference` sans réémettre la valeur refusée.
- Défaut migré vers `env:GENENGINE_AI_AZURE_FOUNDRY_KEY` ; variable câblée vide dans
  `compose.yaml` (Authoring, PlayerExperience) et `.env.example`.
- Grammaire et extensibilité documentées dans
  [`platform-configuration.md`](platform-configuration.md#références-de-secrets).

**Ce qui est testé** (`tests/GenEngine.Services.Tests/SecretResolutionTests.cs` ; 226 tests
backend au vert après fusion de `main`) : résolution réussie depuis l'environnement ; secret absent → fournisseur
non utilisable, aucun fragment de référence dans la raison ; backend qui lève → rabattu sur
`secret_not_found` ; huit grammaires invalides rejetées ; rejet explicite à l'`Upsert` ;
`vault:` dégradé en `secret_scheme_unsupported` ; fournisseur `Offline` et fournisseur
désactivé. Le test central `NoSecretCanReachAClientProjectionAnAvailabilityPayloadOrALog`
résout **réellement** le secret, puis prouve son absence des deux surfaces anonymes
(`GET /experience` et le `GET /client-bootstrap` livré par #52), du payload de disponibilité
et de sept chemins de rendu de log.

Le test `AdminViewKeepsTheCompleteDocumentTheAnonymousRouteNoLongerServes` livré par #52
figeait l'ancienne valeur littérale `"azure-foundry-credential"` ; il a été recalé sur le
nouveau défaut et vérifie en plus que toute référence par défaut respecte la grammaire, pour
qu'un défaut illisible par tout résolveur échoue au test plutôt qu'à l'exécution.

**Ce qui n'est pas testé et ne doit pas être présenté autrement** : aucun client de
coffre-fort n'est livré. Le schéma `vault` est *réservé*, pas implémenté — il n'existe aucun
code à tester, et rien ici n'a été validé contre un coffre-fort réel. Aucun appel à un
fournisseur IA réel n'a été exercé.

**Branchement avec `IAssistantAiClient`** (fusionné entre-temps par #56) : les deux contrats
s'accordent sans adaptation. `IAssistantAiClient.IsConfigured` correspond exactement à
`AiProviderAvailability.IsUsable`, et le `null` que le port impose de retourner plutôt que de
lever correspond à la règle « l'échec est une valeur » de `SecretResolution`. La documentation
du port dit déjà qu'une implémentation « resolves its own credentials locally » : c'est
précisément ce que `SecretStore` fournit, et c'était la pièce manquante.

**Le câblage reste toutefois à faire, et rien ici ne doit être lu autrement.** Le seul
`IAssistantAiClient` enregistré est `OfflineAssistantAiClient`, dont `IsConfigured` est
`false` en dur ; aucun chemin de `PlayerExperience` n'appelle aujourd'hui `SecretStore`.
Brancher la résolution suppose de livrer un client de fournisseur réel — ce que cette tranche
s'interdit faute de pouvoir le vérifier. Enregistrer un `SecretStore` inutilisé dans la DI de
`PlayerExperience` aurait ajouté un composant sans consommateur, ce que les instructions du
dépôt proscrivent ; la dépendance sera ajoutée en même temps que le client qui s'en sert.

Contexte livré au jalon 3 :

- `HRD-004` audit : `IAuditLog` dans `GenEngine.Observability`, émis à la frontière Api ; `specs/process/audit.md`.
- `HRD-005` résilience : `Microsoft.Extensions.Http.Resilience` sur l’appel `Play → Authoring` ; `specs/process/resilience.md`.
- `HRD-006` sauvegarde/restauration chiffrée : outillage shell sous `scripts/`, chiffrement `age` ; `specs/process/backup-restore.md`.
- `HRD-007` : décision « pas d’outbox » actée dans l’ADR 0004.

## Décisions à préserver

- Pas de monolithe ni de base partagée.
- Pas de service réseau pour le moteur Narrative.
- Pas de transaction distribuée.
- Pas d’IA dans le chemin déterministe.
- Pas de fonctionnalité sans paramètres/défauts, permissions et comportement désactivé explicités.
- Pas de rôle métier codé en dur : les règles testent des permissions stables et des scopes, les rôles sont personnalisables.
- Pas de contrôle RBAC uniquement côté client ; allow/deny et isolation de front sont testés côté serveur.
- Pas de dépendance cloud ou IA pour jouer, développer ou exécuter la CI.
- Pas d’outbox anticipée.
- Pas de dépendance ajoutée sans besoin, maintenance et licence acceptables.
- Toute évolution structurante nécessite un ADR.

## Zones à surveiller

- Le pack `diapason-core` est publié et servi par `Configuration` (`GET /asset-packs`, `GET /asset-packs/{packId}`, `GET /asset-packs/{packId}/files/{chemin}`, toutes anonymes). Le Studio du client web y choisit un asset et l'écoute. Ce qui **n'est toujours pas câblé** : aucune étape de scénario ni aucun emplacement d'application n'applique un média côté joueur au runtime backend — le bloc `media` est validé et publié, sa consommation reste au client.
- Le pack ne contient **ni ambiance, ni illustration, ni musique longue** : Kenney n'en publie pas sous CC0. Ces manques sont déclarés dans le champ `gaps` du manifeste et ne doivent pas être comblés par une source dont la licence n'est pas vérifiée à la source.
- L'usage attribué aux quatre `stinger.*` (champ `review` du manifeste) repose sur le nom des fichiers amont, pas sur une écoute ; une passe humaine reste à faire.

- Bug hors périmètre repéré pendant `HRD-003` : `POST /auth/login` (Identity) renvoie `500 internal_error` sur identifiants invalides ou corps vide, au lieu de `401`. Ces 5xx polluent le budget d’erreur ; à corriger dans un lot dédié Identity.
- `Play` appelle l’API interne d’`Authoring` ; cet appel est désormais protégé par une politique de résilience (`HRD-005`, `specs/process/resilience.md`).
- Les logs EF des health checks sont nombreux dans Loki ; ne réduis leur niveau qu’après avoir préservé la capacité de diagnostic.
- Les ports PostgreSQL et observabilité sont exposés pour le développement local, pas comme modèle de production.
- `GenEngine.Services.Tests` est actuellement un projet de test sans test découvert ; ne le présente pas comme une couverture effective.
- Le dashboard d’observabilité repose sur les noms de métriques OpenTelemetry actuels ; toute montée de version doit les revérifier.
- Les interactions facultatives (schéma v4) ne sont **pas encore rendues par les clients** : `GET /sessions/{id}/current-step` expose `isOptional` et `exitChoices`, mais tant qu'un client ignore `exitChoices` une interaction facultative reste vécue comme obligatoire. Le contenu Diapason est en schéma v2 et n'en déclare aucune ; le câblage client et l'usage éditorial restent à faire.

## Critère de passage de relais réussi

Un nouvel agent doit pouvoir cloner le dépôt, lire `AGENTS.md`, lancer les commandes ci-dessus et reprendre P0 du jalon 4 sans dépendre de l’historique de conversation qui a créé le projet.
