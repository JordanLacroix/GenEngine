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

**Statut : terminé.** Observabilité OTLP + stack locale (Collector, Prometheus, Tempo, Loki, Grafana), SLO/alertes et budget d’erreur (`HRD-003`), audit métier sans fuite (`HRD-004`), résilience interservices (`HRD-005`), sauvegarde/restauration chiffrée (`HRD-006`). L’outbox (`HRD-007`) est écartée par décision documentée faute de consommateur asynchrone (ADR 0004).

Le suivi détaillé se trouve dans [`modules/hardening/tasks.md`](modules/hardening/tasks.md).

**Étape active : jalon 4 (« première expérience produit »), désormais cadré par la roadmap fonctionnelle du moteur.** Le passage de relais et les critères de reprise sont détaillés dans [`handoff.md`](handoff.md).

## Jalon 4 — première expérience produit

Rendre les capacités du moteur accessibles aux clients Web et iOS par tranches verticales, sans dupliquer les règles narratives dans les interfaces.

**Statut : en cours.** La première tranche expose le catalogue public des dernières versions publiées (`AUT-006`). La priorité revient désormais à la profondeur fonctionnelle du moteur : état joueur riche, conditions/effets explicables, interactions typées, migrations et exploration. Le détail est suivi dans [`functional-roadmap.md`](functional-roadmap.md). Les clients évolueront après stabilisation de ces contrats.
