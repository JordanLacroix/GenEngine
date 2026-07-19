# Format de scénario

Un `ScenarioDocument` contient `schemaVersion`, `title`, `initialNodeId` et une liste de nœuds. Le schéma v1 conserve les choix portés directement par le nœud. Le schéma v2 peut définir une séquence `interactions` typée. Le schéma v3 ajoute des médias optionnels sur les nœuds et les choix. Le schéma v4 ajoute le drapeau `isOptional` qui rend une interaction facultative ; les quatre formats restent exécutables.

Les interactions v2 disponibles sont `narration`, `quiz`, `choiceSet`, `characteristicGate` et `freeText`. Une narration progresse par une commande continue, un quiz applique des effets corrects ou incorrects sans révéler la réponse dans l'état courant, et un ensemble de choix termine un nœud non final en ciblant le nœud suivant. Un gate évalue automatiquement une condition, applique les effets de réussite ou d'échec, puis entre dans la branche correspondante sans consommer un tour joueur. Une saisie libre compare de façon déterministe et insensible à la casse/aux accents les termes attendus ; son résultat doit être confirmé avant d'appliquer les effets. Un nœud final peut dérouler ses interactions avant de passer à `Completed`.

Les conditions autorisées sont : `always`, `all`, `any`, `not`, `variableEquals`, `variableAtLeast`, `hasItem`, `hasEvidence`, `relationAtLeast`, `hasReward`, `visitedNode` et `characteristicAtLeast`. Les effets autorisés sont : `assign`, `increment`, `collect`, `removeItem`, `discoverEvidence`, `changeRelation`, `grantReward`, `recordNotableEvent`, `schedule`, `advanceLogicalTime`, `emitExternalEvent`, `setCharacteristic` et `changeCharacteristic`. Tout discriminant inconnu est refusé ; aucun script ou type arbitraire n'est exécuté.

Un effet `schedule` peut attendre un nombre de tours, un nombre de jours logiques et une condition déclarative. Les trois contraintes renseignées doivent être satisfaites ; une condition encore fausse conserve l'effet en attente et elle est réévaluée après chaque transition. `advanceLogicalTime` fait progresser uniquement l'horloge déterministe de la partie, jamais l'heure système. Les déclenchements simultanés sont ordonnés par tour, jour logique puis ordre de programmation et protégés par un budget borné.

Le runtime conserve également les caractéristiques extensibles du joueur, l'historique ordonné des choix et interactions ainsi qu'un journal d'événements notables. Les gates automatiques sont historisés avec leur résultat et protégés contre les cycles automatiques non bornés. Une analyse de texte ne persiste pas la saisie brute : uniquement les termes reconnus, le seuil et l'explication ; un refus revient à la saisie sans consommer de tour. `NarrativeRuntime.ExplainChoices` fournit une explication arborescente sans modifier l'état. `ScenarioAnalyzer.Explore` simule les branches, quiz et saisies déterministes atteignables dans un budget borné.

`ITextInputAnalyzer` permet à Application de substituer une analyse externe au fallback `KeywordTextInputAnalyzer`. Le moteur n'accepte que le résultat borné correspondant à la rubrique courante, le fige dans `PendingTextAnalysis`, puis exige toujours la confirmation existante avant progression. La saisie brute et le fournisseur utilisé ne pénètrent pas dans l'état déterministe.

`emitExternalEvent` ajoute à l'état un contrat ordonné (`sequence`, nom, attributs bornés, tour et jour logique). Il ne contacte aucun service et ne constitue pas une outbox : aucun dispatch, retry ou acquittement n'existe dans Narrative. Un futur orchestrateur pourra traduire ces contrats vers un consommateur réel sans introduire ce couplage dans le moteur.

Les sessions persistées utilisent une enveloppe `GameSave` v2 contenant le schéma du scénario, la version du runtime, la graine, l'horodatage, l'historique des migrations et l'état déterministe complet. Le lecteur applique les migrations enregistrées dans l'ordre ; il accepte les enveloppes v1 ainsi que les anciens états bruts, transformés successivement en enveloppes v1 puis v2. Toute écriture utilise exclusivement le format courant.

## Médias (schéma v3)

Un nœud peut porter un objet `media` (`visualUrl`, `visualDescription`, `soundUrl`) et un choix un objet `media` (`soundUrl`, `animationCue`). Tous les champs sont facultatifs et l'objet entier peut être absent.

Ces médias sont des **références** : le moteur ne les résout jamais, ne les télécharge pas et ne les joue pas. Ils n'entrent dans aucune décision, aucun effet et aucun état déterministe, donc ils ne modifient ni le replay ni la sémantique d'un scénario. `animationCue` est un identifiant opaque que le client associe à sa propre animation ; le moteur ne lui donne aucun sens.

Un scénario sans aucun média reste intégralement jouable et compréhensible : le texte narratif porte seul l'information. `visualDescription` fournit une alternative textuelle à l'illustration, jamais un substitut au texte du nœud.

La validation exige une référence d'au plus 2 048 caractères sous l'une des deux formes suivantes (`media_asset_invalid` sinon) : une URL absolue en HTTPS, pour les assets hébergés par l'instance ; ou une référence de pack `packId:assetId`, résolue par le client via le manifeste du pack livré avec la configuration — c'est cette seconde forme qui permet à une démonstration de fonctionner sans aucun serveur d'assets. Les deux segments d'une référence de pack se limitent aux minuscules, chiffres, point, tiret et souligné, afin qu'une référence ne puisse jamais être confondue avec une URL ou un chemin, une `animationCue` non vide d'au plus 64 caractères (`media_animation_cue_invalid`) et une `visualDescription` d'au plus 500 caractères (`media_description_too_long`). Un média déclaré sur un document antérieur au schéma v3 est refusé (`media_requires_schema_3`).

Comme les champs sont facultatifs et omis à la sérialisation lorsqu'ils sont nuls, un document qui ne les utilise pas produit exactement les mêmes octets canoniques et le même hash qu'avant leur introduction. Une fixture golden fige le hash d'un snapshot v2 calculé avant le changement afin de garantir cette propriété.

## Interactions facultatives (schéma v4)

Une interaction porte un drapeau `isOptional`. **Absent ou `false`, l'interaction est obligatoire** : c'est le comportement historique, et un document antérieur au schéma v4 garde donc exactement le déroulé qu'il avait.

Une interaction déclarée `isOptional: true` est présentée **en même temps que la sortie du nœud**, c'est-à-dire son `choiceSet` terminal, au lieu de la masquer. Le joueur choisit alors librement :

- **la jouer** : ses effets s'appliquent et l'exécution revient sur le même nœud, à l'interaction suivante. Comme les effets ont modifié le monde, un choix porteur d'une `condition` peut désormais apparaître — c'est ainsi qu'une interaction jouée « débloque des lignes de dialogue distinctes ». Aucun mécanisme spécifique n'est nécessaire : `ContinueEffects`, `CorrectEffects`/`IncorrectEffects` ou `AcceptedEffects`/`RejectedEffects` posent un marqueur, et la `condition` d'un choix le teste ;
- **l'ignorer** : prendre un choix de sortie quitte le nœud immédiatement. L'interaction non jouée ne laisse **aucune trace** dans `interactionHistory` — seul le `choiceSet` de sortie est historisé — donc toute condition portant sur ce qu'elle aurait accordé reste fausse.

### Nœud mêlant facultatif et obligatoire

La règle est volontairement conservatrice, pour que le séquencement reste non ambigu et rejouable : **la sortie n'est offerte que si toutes les interactions comprises entre l'index courant et le `choiceSet` terminal sont facultatives.**

Une interaction obligatoire continue donc de bloquer, exactement comme avant. Dans un nœud `[narration facultative, quiz obligatoire, choiceSet]`, la narration ne montre aucune sortie : le joueur ne peut pas sauter par-dessus le quiz. Une fois le quiz répondu, l'index avance et la sortie redevient atteignable. Le parcours reste une simple marche avant sur `interactionIndex`, sans état supplémentaire : une session se rejoue à partir de ses seules entrées enregistrées.

Une interaction `freeText` facultative laisse la session en `AwaitingExternalInput` ; prendre un choix de sortie y est explicitement autorisé, sans quoi le statut la rendrait obligatoire de fait. En revanche, tant qu'une analyse attend sa validation (`AwaitingValidation`), la sortie est retirée : le joueur termine ou refuse ce flux avant de pouvoir quitter, ce qui évite qu'une même entrée s'interprète de deux façons.

### Validation

- `optional_requires_schema_4` : `isOptional` déclaré sur un document antérieur au schéma v4 ;
- `optional_interaction_not_supported` : `isOptional: true` sur un `choiceSet` ou un `characteristicGate`. Un `choiceSet` permet déjà de partir et un gate se résout sans entrée joueur : les rendre facultatifs rendrait la sortie ambiguë. Seuls `narration`, `quiz` et `freeText` sont concernés ;
- `optional_requires_exit_choice_set` : `isOptional: true` dans un nœud qui ne se termine pas par un `choiceSet` — un nœud final, par exemple. Rien n'y serait sautable, donc le drapeau promettrait une sortie inexistante.

Le drapeau est nullable et omis à la sérialisation lorsqu'il n'est pas renseigné : un document qui ne l'utilise pas produit exactement les mêmes octets canoniques et le même hash qu'avant son introduction. Une fixture golden fige le hash d'un snapshot v3 et son état final rejoué, tous deux calculés avec le moteur d'avant le changement.

L'exemple exécutable est [`examples/optional-aside.json`](examples/optional-aside.json).

Les brouillons v1 importés ou remplacés dans Authoring sont migrés en v2 avant stockage, sans réécriture rétroactive des snapshots déjà publiés. Les migrations refusent les versions et types JSON inconnus, mais restent distinctes de la validation métier afin qu'Authoring puisse conserver puis diagnostiquer un brouillon incomplet via `/validate`. Des fixtures golden figent un scénario v1, une sauvegarde v1 et l'état final attendu après migration puis replay, ainsi qu'un scénario v2, sa sauvegarde et son état final pour vérifier qu'un snapshot publié avant les médias rejoue à l'identique. `NarrativeTreeBuilder` projette un scénario et une sauvegarde en arbre complet avec nœuds courants, visités, inexplorés ou verrouillés.

La validation exige un titre, un point d'entrée existant, des identifiants uniques, des cibles valides, des conditions/effets bien formés et des budgets de profondeur bornés. Les exemples exécutables vivent dans [`examples/`](examples/).

`NarrativeStructureAnalyzer` complète cette validation par un rapport auteur : composantes cycliques et présence d'une sortie garantie, nœuds dont toutes les sorties sont conditionnelles, fins structurellement inatteignables et zones atteignables sans aucun chemin vers une fin. `NarrativeRuntime.PreviewAt` démarre une exécution isolée depuis n'importe quel nœud autorisé par un état injecté, sans modifier cet état source, et applique les effets d'entrée comme lors d'une vraie partie.

`PlayerProjectionBuilder` produit trois vues stables sans modifier l'état : synthèse de progression, collection triée (inventaire, preuves, récompenses) et journal ordonné. Play expose cette projection à destination des clients sans leur transférer la responsabilité des règles.
