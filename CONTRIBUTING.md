# Contribuer à GenEngine

Merci de contribuer à GenEngine. Le dépôt privilégie les changements petits, vérifiables et reliés à un besoin explicite.

## Avant de commencer

- Consultez les issues et discussions existantes.
- Pour un bug ou une fonctionnalité, utilisez le formulaire correspondant.
- Pour une évolution d’architecture significative, ouvrez d’abord une discussion ou une issue afin de valider le besoin.
- Ne publiez jamais une vulnérabilité exploitable dans une issue : utilisez le [signalement privé](https://github.com/JordanLacroix/GenEngine/security/advisories/new).

## Environnement local

Installez le SDK défini dans `global.json`, puis exécutez :

```bash
dotnet restore --locked-mode
dotnet build --no-restore -warnaserror
dotnet test --no-build
```

## Workflow

1. Créez une branche courte depuis `main` : `feat/sujet`, `fix/sujet`, `docs/sujet`.
2. Implémentez une seule préoccupation cohérente.
3. Ajoutez les tests et la documentation nécessaires.
4. Utilisez des commits conventionnels : `type(scope): description`.
5. Ouvrez une pull request en remplissant chaque section pertinente du modèle.
6. Corrigez les contrôles automatiques et résolvez les conversations de revue.

Les titres de PR acceptent : `build`, `chore`, `ci`, `docs`, `feat`, `fix`, `perf`, `refactor`, `revert` et `test`.

## Définition de terminé

Un changement est prêt lorsque :

- le comportement demandé et ses critères d’acceptation sont satisfaits ;
- la solution compile sans warning et les tests pertinents passent ;
- les invariants et frontières de modules restent respectés ;
- les changements d’API, données ou configuration sont documentés ;
- README, specs, ADR et tâches reflètent l’état réel ;
- aucune donnée sensible ni dépendance injustifiée n’est introduite ;
- tous les contrôles GitHub requis sont verts.

## Contributions assistées par IA

Les outils d’IA sont autorisés, mais l’auteur de la PR reste responsable de chaque ligne. Indiquez dans la PR les invariants consultés, les zones à risque et les validations réellement exécutées. Ne fournissez aucun secret, donnée personnelle ou code non publiable à un service externe.

## Architecture et style

- Respectez [`specs/invariants.md`](specs/invariants.md) et [`specs/architecture.md`](specs/architecture.md).
- Préservez le déterminisme et la pureté du moteur narratif.
- N’ajoutez une dépendance que si sa valeur, sa maintenance et sa licence sont acceptables.
- Un changement structurant doit être accompagné d’un ADR.

En participant, vous acceptez le [`CODE_OF_CONDUCT.md`](CODE_OF_CONDUCT.md).
