# Format de scénario

Un `ScenarioDocument` contient `formatVersion`, `title`, `startNodeId` et une liste de nœuds. Chaque nœud possède un identifiant stable, un texte et des choix ordonnés. Un choix cible un autre nœud ou `null` pour terminer la session.

Les conditions autorisées sont : `always`, `all`, `any`, `not`, `variableEquals`, `variableAtLeast`, `hasItem`, `hasEvidence`, `relationAtLeast`, `hasReward` et `visitedNode`. Les effets autorisés sont : `assign`, `increment`, `collect`, `removeItem`, `discoverEvidence`, `changeRelation`, `grantReward`, `recordNotableEvent` et `schedule`. Tout discriminant inconnu est refusé ; aucun script ou type arbitraire n'est exécuté.

Le runtime conserve également l'historique ordonné des choix et un journal d'événements notables. `NarrativeRuntime.ExplainChoices` fournit une explication arborescente sans modifier l'état. `ScenarioAnalyzer.Explore` simule les branches atteignables dans un budget borné.

La validation exige un titre, un point d'entrée existant, des identifiants uniques, des cibles valides, des conditions/effets bien formés et des budgets de profondeur bornés. Les exemples exécutables vivent dans [`examples/`](examples/).
