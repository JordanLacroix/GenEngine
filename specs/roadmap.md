# Roadmap

## Jalon 0 — cadrage

- [x] Écrire trois scénarios JSON représentatifs.
- [x] Prototyper la sérialisation polymorphe.
- [x] Formaliser les invariants du runtime.
- [x] Choisir et versionner le PRNG.
- [x] Définir la canonicalisation et le hash.
- [x] Décider la politique de versioning et de replay.
- [x] Écrire le threat model initial.

## Jalon 1 — moteur en mémoire

Modèle Domain, evaluator, reducer, runtime, machine à états, validation, simulateur et tests déterministes. Les migrations de format commenceront avec le premier schéma v2 ; le runtime v1 refuse explicitement toute version inconnue.

**Statut : terminé.** Le moteur pur couvre les conditions, effets, transitions, pause/reprise, validation, simulation, hash canonique et tests déterministes.

## Jalon 2 — backend jouable

Services Authoring, Play et Identity indépendants, bases PostgreSQL séparées, publication, sessions persistées, idempotence, contrats API et Docker Compose.

**Statut : terminé.** Les trois services disposent chacun de leur Domain, Application, Infrastructure, API, migration EF et base PostgreSQL. Le parcours jouable est couvert par un smoke test Compose.

## Jalon 3 — durcissement

Observabilité complète, audit renforcé, résilience, sauvegarde/restauration et outbox uniquement si un consommateur asynchrone existe.

**Statut : en cours.** Les trois API exportent désormais logs, traces et métriques en OTLP. Une stack locale optionnelle fournit Collector, Prometheus, Tempo, Loki et Grafana. Les prochains lots portent sur les SLO/alertes, l’audit métier, la résilience puis la sauvegarde/restauration.

Le suivi détaillé se trouve dans [`modules/hardening/tasks.md`](modules/hardening/tasks.md).

**Prochaine tâche : `HRD-003`.** Le passage de relais et les critères de reprise sont détaillés dans [`handoff.md`](handoff.md).
