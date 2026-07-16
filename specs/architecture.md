# Architecture

GenEngine est un monolithe modulaire.

## Règles

1. `Narrative` est pur et ne référence aucun autre module, ASP.NET Core ou EF Core.
2. `Authoring` et `Play` peuvent référencer `Narrative`.
3. `SharedKernel` reste minimal et ne contient pas de logique narrative.
4. `Infrastructure` implémente les ports techniques des modules.
5. `Api` compose l'application sans contenir de logique métier.
6. Aucun module ne lit directement les tables d'un autre module.

Les dépendances ajoutées doivent être permissives et compatibles avec un usage commercial.
