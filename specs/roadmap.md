# Roadmap

## Jalon 0 — cadrage

- [ ] Écrire trois scénarios JSON représentatifs.
- [ ] Prototyper la sérialisation polymorphe.
- [ ] Formaliser les invariants du runtime.
- [ ] Choisir et versionner le PRNG.
- [ ] Définir la canonicalisation et le hash.
- [ ] Décider la politique de versioning et de replay.
- [ ] Écrire le threat model initial.

## Jalon 1 — moteur en mémoire

Modèle Domain, evaluator, reducer, runtime, machine à états, migrations, validation, simulateur et tests déterministes.

## Jalon 2 — backend jouable

PostgreSQL, authoring minimal, publication, sessions persistées, idempotence, API, authentification locale et Docker Compose.

## Jalon 3 — durcissement

Observabilité complète, audit renforcé, résilience, sauvegarde/restauration et outbox uniquement si un consommateur asynchrone existe.
