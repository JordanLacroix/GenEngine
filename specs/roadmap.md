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

**Étape active : jalon 4 (« première expérience produit »), désormais centrée sur la configuration exhaustive du moteur et de la plateforme, le RBAC custom, les organisations, l'assistant, l'IA et l'économie.** Le passage de relais et les critères de reprise sont détaillés dans [`handoff.md`](handoff.md).

## Jalon 4 — première expérience produit

Rendre les capacités du moteur accessibles aux clients Web et iOS par tranches verticales, sans dupliquer les règles narratives dans les interfaces.

**Statut : en cours.** Le socle narratif profond est livré. La priorité est maintenant un registre de configuration typé, le RBAC à rôles custom, les organisations école/entreprise/formation, puis l'assistant/familier, les providers IA interchangeables et l'économie/magasin. Le détail est suivi dans [`functional-roadmap.md`](functional-roadmap.md), [`platform-configuration.md`](platform-configuration.md) et [`configuration-catalog.md`](configuration-catalog.md).
