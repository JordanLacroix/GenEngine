# Format de scénario

Un `ScenarioDocument` contient `schemaVersion`, `title`, `initialNodeId` et une liste de nœuds. Le schéma v1 conserve les choix portés directement par le nœud. Le schéma v2 peut définir une séquence `interactions` typée ; les deux formats restent exécutables.

Les interactions v2 disponibles sont `narration`, `quiz` et `choiceSet`. Une narration progresse par une commande continue, un quiz applique des effets corrects ou incorrects sans révéler la réponse dans l'état courant, et un ensemble de choix termine un nœud non final en ciblant le nœud suivant. Un nœud final peut dérouler ses narrations et quiz avant de passer à `Completed`.

Les conditions autorisées sont : `always`, `all`, `any`, `not`, `variableEquals`, `variableAtLeast`, `hasItem`, `hasEvidence`, `relationAtLeast`, `hasReward` et `visitedNode`. Les effets autorisés sont : `assign`, `increment`, `collect`, `removeItem`, `discoverEvidence`, `changeRelation`, `grantReward`, `recordNotableEvent` et `schedule`. Tout discriminant inconnu est refusé ; aucun script ou type arbitraire n'est exécuté.

Le runtime conserve également l'historique ordonné des choix et interactions ainsi qu'un journal d'événements notables. `NarrativeRuntime.ExplainChoices` fournit une explication arborescente sans modifier l'état. `ScenarioAnalyzer.Explore` simule les branches et réponses de quiz atteignables dans un budget borné.

Les sessions persistées utilisent une enveloppe `GameSave` versionnée contenant le schéma du scénario, la graine, l'horodatage et l'état déterministe complet. Le lecteur accepte également les anciens états bruts v1 et les enveloppe en mémoire. `NarrativeTreeBuilder` projette un scénario et une sauvegarde en arbre complet avec nœuds courants, visités, inexplorés ou verrouillés.

La validation exige un titre, un point d'entrée existant, des identifiants uniques, des cibles valides, des conditions/effets bien formés et des budgets de profondeur bornés. Les exemples exécutables vivent dans [`examples/`](examples/).
