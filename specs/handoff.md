# Passage de relais

Dernière mise à jour : 17 juillet 2026.

## État vérifié

- `main` contient le backend jouable distribué et le premier lot du jalon 3.
- Les services `Authoring`, `Play` et `Identity` sont autonomes et disposent chacun de leur PostgreSQL.
- Le moteur `GenEngine.Narrative` est pur, déterministe et partagé comme bibliothèque embarquée.
- Le parcours inscription → connexion → import → validation → publication → session → choix → replay passe avec `scripts/smoke-test.sh`.
- Les trois API exportent logs structurés, traces HTTP et métriques via OpenTelemetry.
- La surcouche locale fournit Collector, Prometheus, Tempo, Loki et Grafana.
- Le dashboard Grafana `GenEngine — Vue d’ensemble` est provisionné.
- `HRD-003` livre des SLI/SLO provisoires : voir `specs/process/slo.md`, les règles Prometheus sous `deploy/observability/rules/` et le dashboard `GenEngine — SLO et budget d’erreur`.
- `HRD-004` livre l’audit métier : primitive `IAuditLog` dans `GenEngine.Observability`, événements émis à la frontière Api des trois services, politique de non-fuite dans `specs/process/audit.md`.
- `HRD-005` équipe l’appel `Play → Authoring` de résilience (timeouts, retry borné, circuit breaker) via `Microsoft.Extensions.Http.Resilience` : voir `specs/process/resilience.md`.
- `HRD-006` livre la sauvegarde et la restauration chiffrées des trois PostgreSQL : scripts `scripts/backup-databases.sh`, `scripts/restore-database.sh` et `scripts/lib/age-crypto.sh` (chiffrement `age`, dumps `pg_dump -Fc`), procédure et test dans `specs/process/backup-restore.md`. Aucun code applicatif modifié.
- La dernière PR fonctionnelle fusionnée avant ce handoff est la PR GitHub `#12`.
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
| Grafana | `http://localhost:3000` | Interface métriques, logs et traces |
| Prometheus | `http://localhost:9090` | Interface et API métriques |
| Loki | `http://localhost:3100` | API uniquement |
| Tempo | `http://localhost:3200` | API uniquement |

Pour arrêter sans supprimer les données :

```bash
docker compose -f compose.yaml -f compose.observability.yaml down
```

N’utilise `--volumes` que si la perte des données locales est explicitement souhaitée.

## Prochaine unité de travail — HRD-007

Objectif : ajouter une outbox **uniquement** si un consommateur asynchrone réel apparaît. Sans besoin consommateur validé, ne rien ajouter (ni bus, ni outbox) et documenter la décision.

Contexte laissé par `HRD-004` à `HRD-006` :

- `HRD-004` audit : `IAuditLog` dans `GenEngine.Observability`, émis à la frontière Api ; `specs/process/audit.md`.
- `HRD-005` résilience : `Microsoft.Extensions.Http.Resilience` sur l’appel `Play → Authoring` ; `specs/process/resilience.md`.
- `HRD-006` sauvegarde/restauration chiffrée : outillage shell sous `scripts/`, chiffrement `age` (phrase secrète dev / destinataires prod, aucune clé committée), sorties dans `backups/` (ignoré par Git) ; `specs/process/backup-restore.md`.

Ne choisis pas silencieusement une bibliothèque ni un mécanisme d’outbox ; annonce toute hypothèse de périmètre.

## Ordre restant du jalon 3

1. `HRD-007` — outbox uniquement si un consommateur asynchrone réel apparaît.

`HRD-004`, `HRD-005` et `HRD-006` sont **livrées**.

## Décisions à préserver

- Pas de monolithe ni de base partagée.
- Pas de service réseau pour le moteur Narrative.
- Pas de transaction distribuée.
- Pas d’IA dans le chemin déterministe.
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

Un nouvel agent doit pouvoir cloner le dépôt, lire `CLAUDE.md`, lancer les commandes ci-dessus et reprendre le travail restant du jalon 3 sans dépendre de l’historique de conversation qui a créé le projet.
