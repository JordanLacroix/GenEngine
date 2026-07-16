# Passage de relais

Dernière mise à jour : 16 juillet 2026.

## État vérifié

- `main` contient le backend jouable distribué et le premier lot du jalon 3.
- Les services `Authoring`, `Play` et `Identity` sont autonomes et disposent chacun de leur PostgreSQL.
- Le moteur `GenEngine.Narrative` est pur, déterministe et partagé comme bibliothèque embarquée.
- Le parcours inscription → connexion → import → validation → publication → session → choix → replay passe avec `scripts/smoke-test.sh`.
- Les trois API exportent logs structurés, traces HTTP et métriques via OpenTelemetry.
- La surcouche locale fournit Collector, Prometheus, Tempo, Loki et Grafana.
- Le dashboard Grafana `GenEngine — Vue d’ensemble` est provisionné.
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

## Prochaine unité de travail — HRD-003

Objectif : définir des SLI/SLO initiaux et des alertes vérifiables pour les trois API.

Découpage recommandé :

1. documenter les SLI de disponibilité, taux d’erreurs et latence dans `specs/process/slo.md` ;
2. distinguer clairement les objectifs provisoires de développement des engagements de production ;
3. ajouter des recording rules et alerting rules Prometheus versionnées sous `deploy/observability/` ;
4. monter les règles dans `compose.observability.yaml` et les charger depuis `prometheus.yaml` ;
5. exclure les routes `/health/*` des calculs de trafic utilisateur ;
6. valider la syntaxe avec `promtool check rules` dans le conteneur Prometheus ;
7. démontrer chaque expression sur les métriques réelles `http_server_request_duration_seconds_*` ;
8. ajouter un dashboard ou des panneaux montrant SLI et budget d’erreur ;
9. documenter la procédure de test et seulement ensuite passer `HRD-003` à `done`.

Ne choisis pas silencieusement des engagements de production. En l’absence de trafic réel et d’attentes produit validées, présente les seuils comme provisoires et explique leur révision future.

## Ordre restant du jalon 3

1. `HRD-003` — SLI, SLO et alertes ;
2. `HRD-004` — audit métier sans secrets ni données personnelles ;
3. `HRD-005` — timeouts, retry borné et circuit breaker pour les appels interservices idempotents ;
4. `HRD-006` — sauvegarde et restauration testées des trois PostgreSQL ;
5. `HRD-007` — outbox uniquement si un consommateur asynchrone réel apparaît.

## Décisions à préserver

- Pas de monolithe ni de base partagée.
- Pas de service réseau pour le moteur Narrative.
- Pas de transaction distribuée.
- Pas d’IA dans le chemin déterministe.
- Pas d’outbox anticipée.
- Pas de dépendance ajoutée sans besoin, maintenance et licence acceptables.
- Toute évolution structurante nécessite un ADR.

## Zones à surveiller

- `Play` appelle actuellement l’API interne d’`Authoring` : `HRD-005` devra traiter précisément cet appel.
- Les logs EF des health checks sont nombreux dans Loki ; ne réduis leur niveau qu’après avoir préservé la capacité de diagnostic.
- Les ports PostgreSQL et observabilité sont exposés pour le développement local, pas comme modèle de production.
- `GenEngine.Services.Tests` est actuellement un projet de test sans test découvert ; ne le présente pas comme une couverture effective.
- Le dashboard d’observabilité repose sur les noms de métriques OpenTelemetry actuels ; toute montée de version doit les revérifier.

## Critère de passage de relais réussi

Un nouvel agent doit pouvoir cloner le dépôt, lire `CLAUDE.md`, lancer les commandes ci-dessus et commencer `HRD-003` sans dépendre de l’historique de conversation qui a créé le projet.
