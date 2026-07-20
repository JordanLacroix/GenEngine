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

### Tranche `feat/document-interaction`

- Le schéma de scénario v6 ajoute l'interaction `document` et la condition `consultedDocument`. Migration chaînée `scenario-v5-to-v6`, validation conditionnée à la constante de capacité `DocumentSchema` — pour l'interaction **et** pour la condition, séparément — et jamais à `LatestSchema`. Le hash canonique d'un snapshot v5 (`028aff60…a591`) et son état final rejoué sont figés depuis le moteur d'avant le changement (commit `43d2e11`, fixture écrite avant toute modification du code) et vérifiés par test.
- Un document porte un titre, une `nature` nommée mais ouverte (`Memo`, `Email`, `Code`, `Diff`, `Log`, `Table`, `Conversation`, `Report`, `Other`), des en-têtes ordonnés facultatifs, un corps en trois formes de blocs seulement (`paragraph`, `lines`, `table`) et un `excerpt` qui déclare honnêtement « N sur M ». `shownUnits` doit être strictement inférieur à `totalUnits` : un document intégral ne déclare pas d'échantillon.
- Consulter est **facultatif** et s'appuie entièrement sur la mécanique `isOptional`/`exitChoices` du schéma v4 — aucun séquencement nouveau. Consulter applique `consultEffects`, historise sous l'`inputId` `consulted` et avance ; ignorer ne laisse aucune trace.
- `consultedDocument` relit `interactionHistory`, que le moteur enregistre et persiste déjà : **aucun nouvel état de monde, aucun changement de format de sauvegarde**. C'est ce qui rend la consultation rejouable exactement.
- `Play` expose `POST /sessions/{id}/document-consultations`, sur le même chemin de commande idempotente que `continue` et `answers` : une commande rejouée n'applique pas ses effets deux fois. `GET /sessions/{id}/current-step` expose `document`, de façon additive. L'arbre et la topologie sont inchangés : un document ne crée aucune arête.
- Diapason utilise réellement la mécanique sur trois natures différentes — la note de service (`Memo`), le correctif bloqué (`Diff`) et la table de 412 candidatures (`Table`, 6 rangées montrées). Les trois sont facultatifs et débloquent chacun un choix conditionné absent pour qui n'a pas lu.
- **Non livré** : aucun client ne rend encore un document. Comme pour `isOptional` au schéma v4, tant qu'un client ignore le champ `document`, l'interaction reste invisible côté joueur. Le câblage Web et iOS reste à faire.

### Tranche `feat/familiar-fieldhelp-finale` — vérifiée le 19 juillet 2026

Trois besoins distincts réunis parce qu'ils vivent dans le même document de configuration. Fusionnée avec `main` après les tranches branding, aide contextuelle, parcours, secrets, interaction document et pagination : 291 tests backend au vert, `dotnet build -warnaserror` propre.

- **Familier réellement personnalisable.** Les axes `writingStyle` et `accent`, jusqu'ici acceptés en texte libre sans aucune validation, sont désormais catalogués comme `form` et `tone`. Cinq axes s'ajoutent : `aura`, `silhouette`, `speechRhythm`, `languageRegister`, `interventionDensity`. Chaque option porte valeur stable, libellé, description de l'effet, jeton d'accent et référence d'asset facultative, donc chaque axe est prévisualisable. Une valeur hors catalogue est refusée (`invalid_familiar_configuration`), un axe inconnu aussi (`unknown_familiar_axis`). `customName` reste libre mais refuse `<`, `>`, `&` et les caractères de contrôle. Compatibilité assurée dans les deux sens : une configuration sans axes voit son catalogue dérivé en conservant les valeurs déjà utilisées, `availableForms`/`availableTones` restent servis mais sont désormais dérivés, et un profil antérieur reste lisible via les quatre colonnes historiques alimentées depuis la nouvelle carte `jsonb`.
- **Aide intégrée par champ.** `GET /admin/configuration/field-descriptors` (`config.read`) sert un descripteur par chemin de champ (`game.name`, `economy.offers[].price`…) avec libellé, description, exemple et contrainte lisible. La granularité retenue est le chemin de champ, documentée dans `specs/configuration-catalog.md`. `ConfigurationFieldCatalogTests` compare le type `ExperienceDocument` parcouru par réflexion au catalogue **dans les deux sens** : un champ non décrit et un descripteur orphelin font tous deux échouer les tests.
- **Scénario de fin.** Bloc `finale` facultatif avec conditions composables `All`/`Any` et cinq types de condition (`ScenariosCompleted`, `CategoryCompleted`, `JourneyCompleted`, `EndingsReached`, `MasteryPercentReached`). L'évaluation est pure, déterministe, en arithmétique entière, et s'appuie exclusivement sur `ScenarioMastery` — **aucun second système de suivi**. Atteindre la fin estampille `finaleId`/`finaleReachedAt` et écrit une entrée de journal une seule fois ; **rien n'est verrouillé** et le modèle ne comporte volontairement aucun drapeau permettant de rendre la fin bloquante. Un test joue un scénario, encaisse une récompense et vérifie l'estampille inchangée après le déclenchement.
- Migration EF `AddFamiliarAxesAndFinale` : trois colonnes ajoutées à `player_profiles`. Le `defaultValue` généré pour la colonne `jsonb` a été corrigé à la main de `""` vers `"{}"` — une chaîne vide n'est pas du JSON valide et les lignes existantes seraient devenues illisibles.
- Le motif `*bin\\Debug/` a été ajouté à `.gitignore` : **il n'y figurait pas** — vérifié à nouveau contre `main` au moment de la fusion, où aucune ligne `Debug` n'existe dans ce fichier. `dotnet ef` crée bien sur macOS un dossier dont le nom contient un antislash littéral, que `[Bb]in/` ne couvre pas.
- Le bloc `finale` est placé **après** `branding` dans les paramètres positionnels d'`ExperienceDocument`, afin que l'appel existant de `CreateDefault` continue de lier `CreateDiapasonBranding()` à `Branding` sans réécriture.
- Le test de complétude des descripteurs a **effectivement détecté** les douze champs `branding` arrivés de `main` et non décrits ; ils sont désormais documentés. C'est la première mise à l'épreuve réelle du mécanisme, et il a fait ce pour quoi il existe. À la fusion suivante il est resté vert, correctement : `feat/secret-resolution` n'ajoute aucun champ au document. Le descripteur d'`aiProviders[].secretReference` a en revanche été réécrit à la main, sa grammaire et son exemple ayant changé — le test couvre la présence d'un descripteur, pas la fraîcheur de son texte, et c'est une limite à connaître.
- La condition de fin livrée par défaut vise la **catégorie** « Autonomie » et non le parcours « Ce qui reste après toi ». Les parcours sont explicitement destinés à être recomposés par chaque client au-dessus des six postures ; y épingler la fin de référence faisait échouer toute réécriture légitime des parcours avec une erreur portant sur la fin. Les six catégories sont l'axe stable. `JourneyCompleted` reste pleinement supporté et testé.
- Le motif d'ignorance des dossiers à antislash de `dotnet ef` est désormais celui de `feat/pagination-search`, placé en fin de `.gitignore` et couvrant aussi `*obj\\Debug/`. Le bloc introduit ici, qui ne couvrait que `bin`, a été retiré : le leur est un sur-ensemble, en garder deux n'aurait servi qu'à les faire diverger.
- La résolution des fichiers de tests a été vérifiée **par comparaison d'ensembles**, pas à l'œil : ensemble des méthodes portant `[Fact]`/`[Theory]` avant et après fusion, puis recoupement avec la liste réellement découverte par le runner. Les 71 tests attendus sur les cinq fichiers concernés sont présents dans les sources **et** exécutés. Un test qui perdrait son attribut disparaîtrait sans faire échouer quoi que ce soit ; seule la seconde vérification l'attrape.
- `CategoryPlan` et `JourneyPlan`, introduits ici pour l'évaluateur de fin, ont été **supprimés** au profit des `CategoryCatalogEntry`/`JourneyCatalogEntry` livrés par `feat/journeys-default-progress` : ils portaient la même information, et deux formes parallèles du même catalogue dans le même assembly n'avaient pas lieu d'être.

**Non livré, assumé** : le câblage des deux clients. Le moteur sert les axes, les descripteurs de champs et la progression vers la fin ; le rendu de l'aperçu par axe, l'aide par champ dans les écrans d'administration et l'écran de fin restent à faire côté Web et iOS, dans leurs dépôts respectifs.

### Tranche `feat/player-stats`

Le joueur porte désormais des statistiques entièrement configurables, cumulées **au niveau joueur** et non session.

- **Bloc `playerStats` dans Configuration.** `enabled` plus une liste de statistiques (`id`, `key`, `label`, `description`, `maximum`), au plus 24, `id` et `key` uniques. Le slug `key` est contraint à `a-z`, `0-9` et `-` sur 40 caractères, le plafond est strictement positif et borné à 1 000 000, libellé et description sont non vides et bornés. Toute violation renvoie `invalid_player_stat`. Six descripteurs d'aide par champ ajoutés ; le test de complétude de `ConfigurationFieldCatalog` les a exigés, comme prévu. La configuration Le Diapason déclare six statistiques, une par posture.
- **Normalisation.** Le bloc est matérialisé comme `media` (`PlayerStats = document.PlayerStats ?? PlayerStatCatalog.CreateDefault()`), mais son défaut est **vide** : tout document publié porte le bloc, et aucune instance ne se voit inventer des statistiques que ses joueurs verraient apparaître sur leur profil. Seul `CreateDefault(frontId)`, la configuration de référence, en déclare.
- **Schéma de scénario v7 : effet `grantPlayerStat`.** Migration chaînée `scenario-v6-to-v7`, validation conditionnée à la constante de capacité `PlayerStatSchema` et jamais à `LatestSchema`. Le hash canonique d'un snapshot v6 (`46332fdf…1a8d`) et son état final rejoué sont figés depuis le moteur d'avant le changement (commit `b7bc549`, fixture écrite et hashée avant toute modification du code) et vérifiés par test.
- **La frontière session → joueur, explicitement.** C'est le point de conception à connaître : tous les autres effets modifient le `WorldState` de la session, celui-ci ne le peut pas — la valeur vit dans `PlayerExperience`, le plafond dans `Configuration`, et le moteur n'a le droit de connaître ni l'un ni l'autre. Le moteur **enregistre donc seulement l'intention** dans `world.externalEvents` sous le nom `player.stat`, exactement le chemin qu'emprunte déjà `economy.reward`. `Play` relaie les seuls événements ajoutés par la commande courante, avec la clé d'idempotence `session:{id}:external:{sequence}`, vers `POST /internal/player-stats`. **Aucun nouveau couplage** : c'est le chemin interservice de `/internal/rewards` et `/internal/progress-events`. Les invariants 3, 4 et 7 sont intacts.
- **Persistance.** Table `player_stat_values` (migration EF `AddPlayerStatValues`), clé `(ProfileId, Key)`, avec la liste `jsonb` des clés d'idempotence déjà appliquées. Une statistique jamais gagnée **n'a pas de ligne** : l'absence *est* zéro, donc ajouter une statistique au catalogue ne demande aucun backfill.
- **Zéro et saturation.** Toute statistique démarre à zéro ; un gain qui dépasserait le plafond **sature** au lieu d'échouer. L'auteur d'un scénario ne peut pas connaître la valeur courante du joueur : conditionner le gain à cette valeur ferait réussir ou échouer le même effet selon l'ordre dans lequel le joueur a joué les scénarios.
- **Exposition.** Champ additif `stats` sur `GET /me/experience`, donc aussi sur `GET /me/experience/bootstrap` qui embarque la même vue. **Aucune route ajoutée** : les statistiques appartiennent à l'état joueur que cette route sert déjà, et une route dédiée aurait obligé un écran de profil à faire deux appels. Chaque entrée porte `label`, `description`, `value` et `maximum` ensemble — un client ne recoupe jamais deux contrats pour dessiner une barre. `value` est reborné à la lecture, sans réécriture, pour le cas d'un plafond abaissé après coup.
- **Gain non apparié = ignoré.** Un gain nommant une statistique que le front ne publie pas, ou arrivant alors que le bloc est désactivé, est ignoré et non refusé. Un scénario est écrit indépendamment de l'instance qui l'exécute ; échouer coûterait au joueur le tour qu'il vient de jouer. `/internal/rewards` lève au contraire, parce que ses règles filtrent sur un joker `*` et qu'un déclencheur non apparié y est réellement exceptionnel.
- **Vérification.** 344 tests au vert (301 avant), `dotnet build -warnaserror` propre, `dotnet format --verify-no-changes` sans écart nouveau. Six mutations volontaires du code livré ont été exécutées et **toutes attrapées** : saturation retirée, garde de schéma rebranchée sur `Schema` au lieu de `PlayerStatSchema`, idempotence retirée, plafond nul accepté, moteur écrivant la statistique dans l'état de session, `Play` cessant de relayer. La première a d'abord **survécu** — le test de saturation lisait la valeur à travers la vue d'expérience, qui reborne elle aussi ; un test au niveau du domaine a été ajouté, et c'est lui qui l'attrape.

**Non livré, assumé** : aucun client ne rend les statistiques, et aucun scénario Diapason n'écrit encore de `grantPlayerStat`. Le catalogue est publié et la mécanique est câblée de bout en bout côté serveur, mais tant qu'un scénario ne déclare pas l'effet, aucune valeur ne bouge en pratique. Les récompenses conditionnelles (hauts faits, titres) et les seuils débloquant des lignes de dialogue sont hors périmètre et font l'objet de lots séparés qui viendront s'appuyer sur ces statistiques.

### Tranche `feat/conditional-rewards`

Les récompenses conditionnelles demandées — « a fait 5 scénarios, gagne un haut fait et titre et des récompenses » — sont livrées, et le modèle de conditions du finale a été **extrait** plutôt que dupliqué.

- **Extraction, pas duplication.** `FinaleConditionType`/`FinaleConditionMode`/`FinaleConditionDefinition` deviennent `ProgressConditionType`/`ProgressConditionMode`/`ProgressConditionDefinition` dans `ProgressConditionCatalog.cs`, avec la validation d'opérandes désormais unique (`ProgressConditionCatalog.Validate`). Côté `PlayerExperience`, `FinaleEvaluator` est réduit à sa seule règle propre — une fin désactivée n'est jamais satisfaite — et délègue à `ProgressConditionEvaluator`, qui reçoit un `ProgressSnapshot` (maîtrise, catalogue, statistiques) au lieu de quatre paramètres. Le finale et les récompenses ne peuvent plus diverger sur ce qu'est une condition valide.
- **Sixième type de condition : `PlayerStatReached`.** Le finale en hérite gratuitement, ce qu'un test vérifie explicitement. La clé est validée contre le catalogue `playerStats` publié par le même document. La valeur est lue brute, jamais rebornée au plafond : un plafond abaissé après coup ne doit pas retirer une récompense acquise.
- **Bloc `rewards`.** Au plus 48 entrées, chacune avec conditions partagées et 1 à 6 `grants`. Les natures sont un enum fermé `Achievement`/`Title`/`Currency` : les deux premières exigent une `reference` en slug et refusent un montant, la troisième exige un montant strictement positif et refuse une référence. Codes `invalid_reward` et `invalid_reward_condition`. 24 descripteurs d'aide ajoutés ; le test de complétude de `ConfigurationFieldCatalog` les a exigés, comme prévu. Normalisation comme `media`/`playerStats`, défaut **vide**.
- **Évaluation et octroi.** Sur `POST /internal/progress-events` **et** `POST /internal/player-stats`, les deux seuls chemins où une condition peut bouger. **Aucune route ajoutée.** L'évaluation n'a délibérément pas lieu à la lecture : un `GET` qui estampille ferait dépendre la date d'obtention de l'ouverture d'un écran et transformerait chaque lecture en écriture. Table `player_earned_rewards` (migration EF `AddEarnedRewards`, clé `(ProfileId, RewardId)`) : **elle ne stocke que l'estampille**, jamais une progression, qui reste recalculée depuis la maîtrise et les statistiques existantes.
- **Exposition.** Champ additif `rewards` sur `GET /me/experience`, donc aussi sur `bootstrap`. Chaque entrée porte la progression **par condition** (`current`/`target`/`satisfied`), y compris une fois obtenue — même niveau de service que `finale`, aucune régression.
- **Diapason** déclare une récompense : cinq scénarios terminés → haut fait, titre et 100 Accords. Elle ne dépend volontairement **pas** d'une statistique, pour la raison déjà actée sur le finale : une condition référence le catalogue du même document, donc y épingler une clé de `playerStats` ferait échouer toute réécriture légitime des statistiques avec une erreur portant sur les récompenses. C'est un piège qui a été **réellement rencontré** — deux tests de `PlayerStatTests` sont tombés — avant d'être corrigé par ce choix.
- **Vérification.** 370 tests au vert (344 avant), `dotnet build -warnaserror` propre. Sept mutations volontaires exécutées : trois attrapées d'emblée (statistique lue à zéro, clé non validée contre le catalogue, évaluation déplacée sur la lecture), deux attrapées **après ajout de tests** que leur survie a révélés manquants (redatage de l'estampille, crédit de toute nature d'octroi), deux **survivantes assumées** — la garde « déjà obtenue » de `EvaluateRewards` et le test `Kind != Unknown` sont redondants avec, respectivement, la garde du domaine et le repli `(0, 1)`, et aucun test ne peut les distinguer parce qu'ils ne changent aucun comportement observable.

**Non livré, assumé** : aucun client ne rend les récompenses ; le câblage Web et iOS reste à faire. Les seuils de statistiques débloquant des lignes de dialogue restent hors périmètre.

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
- Les interactions facultatives (schéma v4) ne sont **pas encore rendues par les clients** : `GET /sessions/{id}/current-step` expose `isOptional` et `exitChoices`, mais tant qu'un client ignore `exitChoices` une interaction facultative reste vécue comme obligatoire. Le câblage client reste à faire. Depuis `feat/document-interaction`, trois scénarios Diapason déclarent enfin des interactions facultatives : les trois documents. Le même manque de rendu client s'y applique donc, et le champ `document` de l'étape courante n'est lu par aucun client.

## Critère de passage de relais réussi

Un nouvel agent doit pouvoir cloner le dépôt, lire `AGENTS.md`, lancer les commandes ci-dessus et reprendre P0 du jalon 4 sans dépendre de l’historique de conversation qui a créé le projet.
