# Passage de relais

Dernière mise à jour : 18 juillet 2026.

## État vérifié

- `main` contient le backend jouable distribué ; la branche `feat/organization-runtime-scale` porte la tranche d'exploitation multi-organisation en cours de revue.
- Les six services `Authoring`, `Play`, `Identity`, `Configuration`, `PlayerExperience` et `Organization` sont autonomes et disposent chacun de leur PostgreSQL.
- Le moteur `GenEngine.Narrative` est pur, déterministe et partagé comme bibliothèque embarquée.
- Le parcours inscription → connexion → import → validation → analyse → prévisualisation → publication → session → choix → replay passe avec `scripts/smoke-test.sh`.
- Le moteur couvre l'état joueur riche, les interactions typées, les gates de caractéristiques, le texte libre confirmé, les sauvegardes versionnées avec migrations chaînées, les effets différés conditionnels avec date logique et l'arbre de session.
- Play expose une projection joueur stable regroupant synthèse, collection et journal.
- Le moteur accepte une analyse d'entrée substituable mais validée contre la rubrique, et représente les effets externes par des événements ordonnés sans I/O.
- Authoring expose l'analyse des boucles, sorties garanties, impasses conditionnelles et fins inatteignables, ainsi que la prévisualisation depuis un état injecté.
- Les trois API exportent logs structurés, traces HTTP et métriques via OpenTelemetry.
- La surcouche locale fournit Collector, Prometheus, Tempo, Loki et Grafana.
- Le dashboard Grafana `GenEngine — Vue d’ensemble` est provisionné.
- `HRD-003` livre des SLI/SLO provisoires : voir `specs/process/slo.md`, les règles Prometheus sous `deploy/observability/rules/` et le dashboard `GenEngine — SLO et budget d’erreur`.
- `HRD-004` livre l’audit métier : primitive `IAuditLog` dans `GenEngine.Observability`, événements émis à la frontière Api des trois services, politique de non-fuite dans `specs/process/audit.md`.
- `HRD-005` équipe l’appel `Play → Authoring` de résilience (timeouts, retry borné, circuit breaker) via `Microsoft.Extensions.Http.Resilience` : voir `specs/process/resilience.md`.
- `HRD-006` livre la sauvegarde et la restauration chiffrées des trois PostgreSQL : scripts `scripts/backup-databases.sh`, `scripts/restore-database.sh` et `scripts/lib/age-crypto.sh` (chiffrement `age`, dumps `pg_dump -Fc`), procédure et test dans `specs/process/backup-restore.md`. Aucun code applicatif modifié.
- La dernière PR fonctionnelle fusionnée est la PR GitHub `#33`, qui livre l'analyse d'entrée substituable et les contrats d'effets externes.
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

- ADR 0005 et service `Organization` DDD/Clean avec PostgreSQL indépendant.
- CRUD audité des unités, memberships et affectations, scopes de front signés et résolution `/me`/interservice.
- contrôle allow/deny dans Play sur les affectations directes de scénario ou de catégorie ; front figé dans le snapshot et la session.
- écrans d'exploitation Web/iOS séparés du studio, avec création, listes et suppressions utiles.
- tests d'isolation croisée, validité temporelle, hiérarchie cyclique et contrôle du démarrage.

Ce qui reste explicitement hors de cette tranche : périodes métier nommées, import/export de masse, historique des memberships, résolution d'une affectation de parcours complet, projection du catalogue filtré, héritage multi-portée global, snapshot de session de l'assistant, metering/quota IA et import Codex Pets.

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

- Bug hors périmètre repéré pendant `HRD-003` : `POST /auth/login` (Identity) renvoie `500 internal_error` sur identifiants invalides ou corps vide, au lieu de `401`. Ces 5xx polluent le budget d’erreur ; à corriger dans un lot dédié Identity.
- `Play` appelle l’API interne d’`Authoring` ; cet appel est désormais protégé par une politique de résilience (`HRD-005`, `specs/process/resilience.md`).
- Les logs EF des health checks sont nombreux dans Loki ; ne réduis leur niveau qu’après avoir préservé la capacité de diagnostic.
- Les ports PostgreSQL et observabilité sont exposés pour le développement local, pas comme modèle de production.
- `GenEngine.Services.Tests` est actuellement un projet de test sans test découvert ; ne le présente pas comme une couverture effective.
- Le dashboard d’observabilité repose sur les noms de métriques OpenTelemetry actuels ; toute montée de version doit les revérifier.

## Critère de passage de relais réussi

Un nouvel agent doit pouvoir cloner le dépôt, lire `CLAUDE.md`, lancer les commandes ci-dessus et reprendre P0 du jalon 4 sans dépendre de l’historique de conversation qui a créé le projet.
