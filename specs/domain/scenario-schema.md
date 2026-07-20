# Format de scénario

Un `ScenarioDocument` contient `schemaVersion`, `title`, `initialNodeId` et une liste de nœuds. Le schéma v1 conserve les choix portés directement par le nœud. Le schéma v2 peut définir une séquence `interactions` typée. Le schéma v3 ajoute des médias optionnels sur les nœuds et les choix. Le schéma v4 ajoute le drapeau `isOptional` qui rend une interaction facultative. Le schéma v5 ajoute un objet `help` facultatif sur les nœuds et les choix. Le schéma v6 ajoute l'interaction `document` et la condition `consultedDocument`. Le schéma v7 ajoute l'effet `grantPlayerStat` ; les sept formats restent exécutables.

Les interactions v2 disponibles sont `narration`, `quiz`, `choiceSet`, `characteristicGate` et `freeText`. Une narration progresse par une commande continue, un quiz applique des effets corrects ou incorrects sans révéler la réponse dans l'état courant, et un ensemble de choix termine un nœud non final en ciblant le nœud suivant. Un gate évalue automatiquement une condition, applique les effets de réussite ou d'échec, puis entre dans la branche correspondante sans consommer un tour joueur. Une saisie libre compare de façon déterministe et insensible à la casse/aux accents les termes attendus ; son résultat doit être confirmé avant d'appliquer les effets. Un nœud final peut dérouler ses interactions avant de passer à `Completed`.

Les conditions autorisées sont : `always`, `all`, `any`, `not`, `variableEquals`, `variableAtLeast`, `hasItem`, `hasEvidence`, `relationAtLeast`, `hasReward`, `visitedNode` et `characteristicAtLeast`. Les effets autorisés sont : `assign`, `increment`, `collect`, `removeItem`, `discoverEvidence`, `changeRelation`, `grantReward`, `recordNotableEvent`, `schedule`, `advanceLogicalTime`, `emitExternalEvent`, `setCharacteristic`, `changeCharacteristic` et `grantPlayerStat`. Tout discriminant inconnu est refusé ; aucun script ou type arbitraire n'est exécuté.

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
- `optional_interaction_not_supported` : `isOptional: true` sur un `choiceSet` ou un `characteristicGate`. Un `choiceSet` permet déjà de partir et un gate se résout sans entrée joueur : les rendre facultatifs rendrait la sortie ambiguë. Seuls `narration`, `quiz`, `freeText` et `document` sont concernés ;
- `optional_requires_exit_choice_set` : `isOptional: true` dans un nœud qui ne se termine pas par un `choiceSet` — un nœud final, par exemple. Rien n'y serait sautable, donc le drapeau promettrait une sortie inexistante.

Le drapeau est nullable et omis à la sérialisation lorsqu'il n'est pas renseigné : un document qui ne l'utilise pas produit exactement les mêmes octets canoniques et le même hash qu'avant son introduction. Une fixture golden fige le hash d'un snapshot v3 et son état final rejoué, tous deux calculés avec le moteur d'avant le changement.

L'exemple exécutable est [`examples/optional-aside.json`](examples/optional-aside.json).

## Aide d'auteur (schéma v5)

Un nœud et un choix peuvent porter un objet `help` facultatif, dont chaque champ est lui aussi facultatif :

| Champ | Modalité | Contenu attendu |
|---|---|---|
| `hint` | `Hint` | un indice discret qui ne nomme pas la réponse |
| `objective` | `Objective` | l'objectif courant reformulé |
| `consequence` | `Consequence` | les conséquences que le joueur est censé déjà connaître |
| `blocker` | `Blocker` | pourquoi une option visible est indisponible |

Ces champs sont **de présentation uniquement**. Le moteur ne les lit pas, ne branche pas dessus et n'en dérive aucun état : une étape doit rester entièrement jouable quand un client, ou la politique d'assistant, ignore l'objet entier. Les séparer plutôt que les fondre en un seul texte permet à la politique de ne servir que ce que le niveau d'aide du joueur autorise — un indice discret et l'explication d'une condition bloquante ne divulguent pas la même chose.

C'est `PlayerExperience` qui les consomme, en relisant la version publiée via la route interne d'Authoring ; voir [`../player-experience.md`](../player-experience.md).

### Validation

- `help_requires_schema_5` : `help` déclaré sur un document antérieur au schéma v5 ;
- `help_text_invalid` : un champ d'aide vide ou de plus de 500 caractères. La borne évite qu'un document ne fasse transiter une charge non bornée vers un fournisseur d'IA.

L'objet est nullable, comme chacun de ses champs, et omis à la sérialisation lorsqu'il n'est pas renseigné : un document qui ne l'utilise pas produit exactement les mêmes octets canoniques et le même hash qu'avant son introduction. Une fixture golden fige le hash d'un snapshot v4 et son état final rejoué, tous deux calculés avec le moteur d'avant le changement.

## Documents (schéma v6)

Les scénarios évoquaient jusqu'ici des documents — une note de service, un correctif bloqué, une table de 412 candidatures — sans jamais les montrer. L'interaction `document` les présente réellement. Le contenu est **porté par le scénario**, donc versionné, hashé et rejoué comme le reste : le moteur ne va rien chercher.

```jsonc
{
  "$type": "document",
  "id": "la-note",
  "isOptional": true,
  "prompt": "Ouvrir la note et la lire en entier",
  "document": {
    "title": "Réorganisation du périmètre Données",
    "nature": "Memo",
    "headers": [{ "name": "Objet", "value": "…" }],
    "excerpt": { "shownUnits": 4, "totalUnits": 27, "unit": "Paragraphs" },
    "blocks": [{ "$type": "paragraph", "text": "…" }]
  },
  "consultEffects": [{ "$type": "discoverEvidence", "evidence": "note-lue-integralement" }]
}
```

### Nature

`nature` nomme ce que le document **est** : `Memo`, `Email`, `Code`, `Diff`, `Log`, `Table`, `Conversation`, `Report`, et `Other`. La taxonomie est nommée pour qu'un client puisse rendre une note comme une note et un journal comme un journal ; `Other` la garde ouverte, pour qu'une nature imprévue n'impose pas une montée de schéma.

### Corps

Le corps est une liste de `blocks`, et le vocabulaire est **délibérément arrêté à trois formes** :

| Bloc | Rend | Sert |
|---|---|---|
| `paragraph` | de la prose | note de service, courriel, commentaire |
| `lines` | des lignes, chacune avec un `marker` et un `label` facultatifs | diff (`Added`, `Removed`, `Context`), journal applicatif (`Warning`, `Error`), extrait de code |
| `table` | `columns` + `rows` | table de données |

C'est le plus petit vocabulaire qui permet encore un rendu client correct — une table se rend en table, un diff en diff, un courriel avec ses en-têtes. Aller plus loin reviendrait à livrer un langage de balisage, que le moteur n'a pas à interpréter ; s'arrêter à une chaîne libre jetterait une structure qui porte du sens. Un même jeu de `marker` couvre le diff et le journal, ce qui évite deux types de blocs pour une seule forme.

`headers` porte les en-têtes ordonnés (`De`, `Objet`, `Fichier`…), ou reste absent quand la nature n'en a pas.

### Échantillon

`excerpt` déclare honnêtement qu'on ne montre qu'une partie : `shownUnits` sur `totalUnits`, dans une `unit` (`Lines`, `Rows`, `Messages`, `Entries`, `Paragraphs`). Un client en tire « 6 lignes affichées sur 412 ». **Un échantillon présenté comme un tout serait un mensonge d'interface**, et le jeu porte précisément sur la lucidité face à l'information. Un document montré intégralement ne déclare **pas** d'`excerpt` : `shownUnits` doit être strictement inférieur à `totalUnits`, faute de quoi la mention ne dirait rien.

### Consulter est facultatif, et conséquent

Le document s'inscrit dans la mécanique `isOptional` du schéma v4, sans rien y ajouter : déclaré `isOptional: true`, il est présenté à côté des `exitChoices` du nœud, et **consulter n'est jamais obligatoire**.

- **Consulter** applique `consultEffects`, inscrit la consultation dans `interactionHistory` sous l'`inputId` `consulted`, et avance à l'interaction suivante — exactement comme `continue` sur une narration.
- **Ignorer** quitte le nœud par un choix de sortie et ne laisse aucune trace.

La condition `consultedDocument` teste cette trace :

```jsonc
{ "$type": "consultedDocument", "interactionId": "la-note" }
```

Elle relit l'historique d'interactions que le moteur enregistre et persiste déjà : **aucun nouvel état, aucun changement de format de sauvegarde**, et un rejeu exact à partir des seules commandes enregistrées. Un joueur qui a lu la note peut donc se voir proposer des choix que celui qui ne l'a pas lue n'a pas — c'est ce qui donne sa valeur pédagogique à la mécanique ; un document purement décoratif n'apprendrait rien.

Consulter est une commande joueur comme une autre : elle consomme un tour et porte une clé d'idempotence côté `Play`, donc une commande rejouée n'applique ses effets qu'une fois. Comme la consultation fait avancer la séquence, le même document ne peut pas être consulté deux fois depuis la même position : un second appel ne trouve plus de document et est refusé (`interaction_not_document`) plutôt que de doubler quoi que ce soit.

### Validation

- `document_requires_schema_6` et `consulted_document_requires_schema_6` : l'interaction ou la condition déclarée sur un document antérieur au schéma v6. Les deux moitiés sont conditionnées **séparément** à la constante de capacité `DocumentSchema`, jamais à `LatestSchema` ;
- `consulted_document_missing` : la condition référence un `interactionId` qui n'est aucun `document` du scénario. Une faute de frappe rendrait sinon un choix définitivement inatteignable ;
- `document_prompt_required`, `document_title_invalid`, `document_blocks_invalid`, `document_headers_invalid`, `document_header_invalid` ;
- `document_paragraph_invalid`, `document_lines_invalid`, `document_line_too_long` ;
- `document_columns_invalid`, `document_column_invalid`, `document_rows_invalid`, `document_row_arity_mismatch` — une ligne dépareillée forcerait chaque client à inventer sa propre règle de remplissage, et le rendu différerait de l'un à l'autre ;
- `document_excerpt_invalid` : `shownUnits` ou `totalUnits` non positif, ou `shownUnits` supérieur ou égal à `totalUnits`.

Toutes les collections sont bornées (64 blocs, 200 lignes, 200 rangées, 12 colonnes, 12 en-têtes, 4 000 caractères par texte) pour qu'un scénario ne fasse pas entrer une charge non bornée dans un snapshot publié.

Les types sont entièrement nouveaux et l'interaction est opt-in : un document qui n'en déclare aucun produit exactement les mêmes octets canoniques et le même hash qu'avant leur introduction. Une fixture golden fige le hash d'un snapshot v5 et son état final rejoué, tous deux calculés avec le moteur d'avant le changement.

## Statistiques joueur (schéma v7)

L'effet `grantPlayerStat` accorde des points d'une statistique **joueur** :

```json
{ "$type": "grantPlayerStat", "stat": "lucidite", "amount": 5 }
```

Il s'écrit partout où un effet s'écrit : `onEnterEffects` d'un nœud, `effects` d'un choix, `continueEffects` d'une narration, `correctEffects`/`incorrectEffects` d'un quiz, `consultEffects` d'un document, `acceptedEffects`/`rejectedEffects` d'une saisie libre, `satisfiedEffects`/`failedEffects` d'un gate, et sous un `schedule`.

### Pourquoi il ne ressemble à aucun autre effet

Tous les autres effets modifient le `WorldState` de **la session**. Celui-ci ne le peut pas : une statistique vit dans `PlayerExperience`, persiste d'un scénario à l'autre, et son plafond est publié par `Configuration` — trois choses que le moteur n'a pas le droit de connaître, puisqu'il n'effectue aucune I/O et doit rester une fonction pure du scénario, du monde et des commandes (invariants 3, 4 et 7).

Le moteur **enregistre donc seulement l'intention**. Appliquer l'effet ajoute à `world.externalEvents` un événement `player.stat` portant les attributs `stat` et `amount`, exactement le chemin que `economy.reward` emprunte déjà. Aucun couplage n'est introduit : la session reste complète et rejouable seule, le déterminisme est intact, et le moteur n'apprend jamais si la statistique existe ni ce qu'elle vaut au bout du compte.

### La frontière session → joueur

1. le moteur enregistre `player.stat` dans `externalEvents` ;
2. `Play` ne relaie que les événements **ajoutés par la commande courante** et en fait un `PlayerStatDispatch` portant la clé d'idempotence `session:{sessionId}:external:{sequence}` — la même famille de clés que les récompenses, donc une commande rejouée ne crédite jamais deux fois ;
3. `Play` appelle `POST /internal/player-stats` sur `PlayerExperience` ;
4. `PlayerExperience` est **la seule autorité** sur la valeur : il la fait démarrer à zéro, l'incrémente et la sature au plafond publié.

Aucun nouveau couplage n'est créé : c'est le chemin interservice déjà emprunté par `POST /internal/rewards` et `POST /internal/progress-events`.

### Validation

- `player_stat_requires_schema_7` : l'effet déclaré sur un document antérieur au schéma v7. La vérification est conditionnée à la constante de capacité `PlayerStatSchema`, jamais à `LatestSchema` ;
- `player_stat_key_invalid` : `stat` vide, de plus de 40 caractères, ou hors du jeu `a-z`, `0-9` et `-`. La grammaire est **la même** que celle de `playerStats.stats[].key` côté configuration : une clé qu'un côté accepte et que l'autre refuse serait une statistique qu'aucun scénario ne pourrait alimenter ;
- `player_stat_amount_invalid` : `amount` nul, négatif ou supérieur à 1 000 000. Le moteur ne connaît pas le plafond configuré ; il ne refuse donc qu'un gain qui ne pourrait jamais rien vouloir dire. La saturation est décidée par `PlayerExperience`, qui détient le plafond.

L'effet est entièrement nouveau et opt-in : un document qui n'en déclare aucun produit exactement les mêmes octets canoniques et le même hash qu'avant son introduction. Une fixture golden fige le hash d'un snapshot v6 et son état final rejoué, tous deux calculés avec le moteur d'avant le changement (commit `b7bc549`).

Les brouillons v1 importés ou remplacés dans Authoring sont migrés en v2 avant stockage, sans réécriture rétroactive des snapshots déjà publiés. Les migrations refusent les versions et types JSON inconnus, mais restent distinctes de la validation métier afin qu'Authoring puisse conserver puis diagnostiquer un brouillon incomplet via `/validate`. Des fixtures golden figent un scénario v1, une sauvegarde v1 et l'état final attendu après migration puis replay, ainsi qu'un scénario v2, sa sauvegarde et son état final pour vérifier qu'un snapshot publié avant les médias rejoue à l'identique. `NarrativeTreeBuilder` projette un scénario et une sauvegarde en arbre complet avec nœuds courants, visités, inexplorés ou verrouillés.

La validation exige un titre, un point d'entrée existant, des identifiants uniques, des cibles valides, des conditions/effets bien formés et des budgets de profondeur bornés. Les exemples exécutables vivent dans [`examples/`](examples/).

`NarrativeStructureAnalyzer` complète cette validation par un rapport auteur : composantes cycliques et présence d'une sortie garantie, nœuds dont toutes les sorties sont conditionnelles, fins structurellement inatteignables et zones atteignables sans aucun chemin vers une fin. `NarrativeRuntime.PreviewAt` démarre une exécution isolée depuis n'importe quel nœud autorisé par un état injecté, sans modifier cet état source, et applique les effets d'entrée comme lors d'une vraie partie.

`PlayerProjectionBuilder` produit trois vues stables sans modifier l'état : synthèse de progression, collection triée (inventaire, preuves, récompenses) et journal ordonné. Play expose cette projection à destination des clients sans leur transférer la responsabilité des règles.
