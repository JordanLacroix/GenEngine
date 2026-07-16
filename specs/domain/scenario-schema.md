# Format de scénario

Un `ScenarioDocument` contient `formatVersion`, `title`, `startNodeId` et une liste de nœuds. Chaque nœud possède un identifiant stable, un texte et des choix ordonnés. Un choix cible un autre nœud ou `null` pour terminer la session.

Les conditions autorisées sont : `always`, `all`, `any`, `not`, `variableEquals`, `variableAtLeast`, `hasItem` et `visitedNode`. Les effets autorisés sont : `assign`, `increment`, `collect` et `schedule`. Tout discriminant inconnu est refusé ; aucun script ou type arbitraire n'est exécuté.

La validation exige un titre, un point d'entrée existant, des identifiants uniques et des cibles valides. Les exemples exécutables vivent dans [`examples/`](examples/).
