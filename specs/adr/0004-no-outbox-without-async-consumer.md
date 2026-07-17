# ADR 0004 — Pas d'outbox tant qu'aucun consommateur asynchrone n'existe

## Statut

Accepté.

## Contexte

La tâche de durcissement `HRD-007` prévoit une outbox transactionnelle **uniquement** si un consommateur asynchrone réel apparaît. Le patron outbox n'a de valeur que pour publier de façon fiable des messages vers un consommateur découplé (bus, file, worker) en cas d'échec après commit.

État vérifié du dépôt au moment de la décision :

- Aucun bus, file, courtier ou service de messagerie (pas de RabbitMQ, Kafka, MassTransit, Azure Service Bus, NATS ni équivalent) — ni dans le code, ni dans `Directory.Packages.props`, ni dans `compose.yaml`.
- Aucun `IHostedService` / `BackgroundService` ni consommateur asynchrone.
- La seule interaction interservices est l'appel HTTP **synchrone** `Play → Authoring` (`GET /internal/scenario-versions/{id}`), déjà fiabilisé par une politique de résilience (`HRD-005`, voir `specs/process/resilience.md`).

## Décision

Ne rien ajouter : **pas d'outbox, pas de bus, pas de table de messages, pas de worker**.

L'introduction d'une outbox est explicitement différée jusqu'à ce qu'un besoin réel apparaisse, c'est-à-dire l'arrivée d'un **consommateur asynchrone validé** (par exemple projection, notification, intégration externe ou tout traitement découplé du cycle requête/réponse).

Le déclencheur de réévaluation est donc concret : dès qu'une fonctionnalité exige de publier un événement de façon fiable vers un consommateur qui ne partage pas la transaction émettrice, cet ADR devra être révisé et l'outbox conçue à ce moment-là (nouvel ADR).

## Conséquences

- On respecte les invariants « Pas d'outbox anticipée » et « N'ajoute ni bus, ni outbox, ni service supplémentaire sans consommateur ou besoin validé ».
- Aucune complexité, dépendance ni surface de maintenance ajoutée sans bénéfice.
- `HRD-007` est considérée comme traitée par cette décision documentée ; le jalon 3 (durcissement) est clos.
- Si un consommateur asynchrone est introduit plus tard, la fiabilité de publication devra être conçue en même temps (outbox ou mécanisme équivalent), sous un nouvel ADR.

## Alternatives écartées

- **Ajouter une outbox « au cas où »** : complexité et maintenance sans consommateur, contraire aux invariants.
- **Introduire un bus léger dès maintenant** : crée un composant sans producteur ni consommateur réel.
- **S'appuyer sur des writes best-effort après commit** : non pertinent en l'absence de tout consommateur ; à traiter le moment venu, avec le consommateur concret en vue.
