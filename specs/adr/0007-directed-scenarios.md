# ADR 0007 — Scénarios dirigés par LLM

## Statut

Proposé. Socle moteur livré ; frontière de service et persistance décrites mais non implémentées (voir « Ce qui n'est pas livré »).

## Contexte

Le dépôt ne connaît qu'une famille de scénarios : le **déterministe**, un graphe de
nœuds figé, hashé canoniquement, rejoué à l'identique (ADR 0003, invariant 4). Un
second besoin apparaît : un scénario où **chaque joueur fait avancer l'histoire à sa
façon**, avec un modèle de langage connecté qui écrit la prose, sans que le modèle
puisse casser la structure.

Un prototype de référence (mono-scénario, en mémoire, sans persistance) démontre la
séparation qui est le cœur du sujet : **le LLM écrit, il ne décide pas.** Un
« narrateur » propose des variations, un « directeur » les valide contre l'état réel
et rejette silencieusement l'incohérent ; un graphe de *beats* porte des prédicats,
et le drapeau de satisfaction renvoyé par le modèle ne suffit jamais — le prédicat
doit passer aussi.

La décision d'architecture est prise en amont : c'est un **nouveau type de scénario,
à côté du déterministe**. Les deux familles coexistent. Les scénarios déterministes
ne bougent pas — graphe figé, hash canonique inchangé, tests golden verts.

## Décision

### 1. Un type de document séparé, pas une capacité du schéma existant

Un scénario dirigé est un `DirectedScenarioDocument` **distinct** de
`ScenarioDocument`, avec son propre couloir de versions `DirectedVersions.Schema`
(indépendant de `NarrativeVersions.LatestSchema`). Ce n'est pas une constante de
capacité ajoutée au schéma déterministe.

Raison : un scénario dirigé n'a ni nœuds, ni choix déclaratifs, ni transitions ; il a
des beats, des prédicats, des faits sous clé et un contrat de narrateur. L'intégrer à
`ScenarioDocument` obligerait soit à y ajouter des champs dirigés nullables — au
risque, à la moindre erreur, de faire dériver le hash de tous les snapshots publiés —
soit à travestir sa forme. Un type séparé garantit que **le document déterministe
n'est jamais touché**, octet pour octet : c'est la garantie la plus forte possible et
elle est vérifiée par le fait qu'aucun fichier existant du moteur n'est modifié et que
les tests de gel de hash (v2 → v7) et les quatre tests golden de rejeu restent verts.

### 2. Prédicats déclaratifs, jamais de code auteur exécuté

Le prototype porte `requires`/`satisfied` comme des fermetures JavaScript. GenEngine
ne peut pas : **l'invariant 8 interdit d'exécuter tout script ou expression arbitraire
fourni par un auteur.** Les prédicats de beat sont donc un arbre `DirectedPredicate`
**déclaratif**, sérialisé avec un discriminant `$type` comme le langage de conditions
déterministe existant, et évalué par le moteur lui-même.

### 3. Le directeur est pur ; le narrateur est un appel de service

La validation d'un tour — `DirectedRuntime.ApplyTurn(document, state, utterance,
proposal)` — est une **fonction pure et déterministe** : mêmes entrées, même état
suivant. Elle ne fait aucune I/O et n'appelle jamais un modèle. La proposition du
modèle est une **entrée explicite**. C'est exactement l'invariant 15 :

> Une sortie IA ne modifie jamais directement l'état narratif ; elle devient une
> entrée explicite, validée et figée avant tout effet.

Ce runtime vit dans le moteur `GenEngine.Narrative`, qui reste pur. Le précédent
existe déjà : `ITextInputAnalyzer` laisse Application substituer une analyse externe
dont le moteur n'accepte que le résultat borné, figé avant progression.

### 4. Le rejeu : ce qu'il garantit désormais, ce qu'il ne garantit plus

Une session dirigée n'est pas déterministe *par nature* : le modèle est stochastique.
La position est explicite :

- **On garantit la cohérence structurelle, toujours.** Aucune proposition du modèle
  ne peut violer le monde : lieu inexistant, objet non possédé, PNJ mort qui
  interagit, fait non révélable ici sont rejetés silencieusement et jamais appliqués,
  même à moitié. Le drapeau `beatSatisfied` ne fait pas avancer un beat si le prédicat
  `satisfied` échoue. L'horloge dure impose les bascules non négociables.
- **On garantit le rejeu exact d'une session enregistrée.** Chaque tour persiste la
  proposition acceptée (`NarratorProposal`) à côté de l'énoncé joueur. Rejouer la même
  suite `(énoncé, proposition)` reproduit **exactement** la même suite d'états, parce
  que le directeur est pur. La proposition enregistrée est la source de vérité.
- **On ne garantit plus** que ré-appeler le modèle reproduise la même prose. Il ne le
  fera pas. Le rejeu d'une session dirigée relit des sorties enregistrées ; il ne
  régénère pas le récit.

Cette position **n'affaiblit en rien** la garantie des scénarios déterministes : type
séparé, runtime séparé, invariant 4 intact.

### 5. Frontière de service : PlayerExperience, pas Play

L'exécution d'un tour dirigé appartient à **PlayerExperience** (à terme au bounded
context `Assistant` planifié dans `architecture.md`), pas à `Play`.

Raison : le modèle de `Play` est le rejeu déterministe de commandes idempotentes
contre un snapshot publié, avec un reducer pur (invariants 3, 4, 7). Un tour dirigé
est fondamentalement un aller-retour vers un modèle dont la sortie doit être persistée
comme source de vérité — ce n'est pas une commande déterministe, et l'y injecter
salirait le reducer de `Play`. PlayerExperience porte déjà le port `IAssistantAiClient`
et la discipline « l'IA est optionnelle, l'échec est une valeur ». Le directeur pur
reste, lui, dans `GenEngine.Narrative`, embarqué par le service.

### 6. Persistance : jamais en mémoire

Le prototype garde tout en mémoire et l'assume. Nous non. Chaque tour persiste une
enveloppe versionnée `{ directedSchema, runtime, état, proposition acceptée, clé
d'idempotence du tour }`, append-only, sur le modèle de discipline de `GameSave` :
une commande de tour rejouée n'applique pas ses effets deux fois. (Interface décrite,
implémentation EF laissée — voir plus bas.)

### 7. Coût et disponibilité : franc, pas optimiste

Un tour = un appel au modèle. Un scénario dirigé **sans fournisseur configuré ne
démarre pas** et le dit clairement, plutôt que de prétendre jouer hors ligne. C'est
compatible avec l'invariant 14 : celui-ci exige qu'une **indisponibilité de l'IA
n'empêche pas le parcours hors ligne prévu**, pas que tout scénario soit jouable hors
ligne. Le parcours hors ligne prévu, ce sont les scénarios déterministes, qui restent
entièrement jouables. Un scénario dirigé est opt-in et n'est jamais l'unique voie de
jeu. Fournisseur lent ou en erreur en cours de partie : le tour échoue proprement, la
session reste sur son dernier état persisté, le joueur réessaie ; aucun état n'est
avancé sans proposition validée.

### 8. Garde-fou d'injection

L'énoncé joueur est **du texte, jamais une instruction**. `DirectedRuntime.Sanitize\
Utterance` le tronque à 400 caractères et retire les caractères de contrôle ; côté
service, il entre dans un message de rôle `user`, jamais dans le contrat système, qui
reste stable d'un tour à l'autre (cache fournisseur) et porte les règles. Un joueur ne
peut pas reprogrammer le narrateur.

## Ce qui est livré

- Le modèle de domaine et le runtime purs dans `GenEngine.Narrative`
  (`DirectedModel.cs`, `DirectedRuntime.cs`, `DirectedValidation.cs`).
- Le contrat de sortie du modèle (`NarratorProposal` / `NarratorDelta`).
- Un scénario Diapason dirigé, `content/diapason/directed/la-nuit-du-seuil.json`,
  validé et joué de bout en bout en test vers deux fins.
- La couverture : rejet silencieux, gate de prédicat, faits sous clé, horloge dure,
  pression, garde-fou d'injection, déterminisme et rejeu depuis propositions
  enregistrées.

## Ce qui n'est pas livré (assumé)

- La frontière de service concrète : DTO, endpoints HTTP de tour, orchestration de
  session dans PlayerExperience.
- La persistance EF de l'enveloppe de session dirigée.
- L'adaptateur de fournisseur réel : il relève du lot `feat/ai-provider-wiring`.
  Ce socle **dépend d'une abstraction** (`IAssistantAiClient`, dont l'unique
  implémentation actuelle a `IsConfigured = false`) et n'en fournit pas de double,
  pour ne pas doubler ce lot. Ce qu'il attend d'elle : produire, depuis un contexte de
  tour, une `NarratorProposal` conforme au schéma, et **retourner l'absence plutôt que
  lever** quand le fournisseur est absent, lent ou en erreur.
- La compression du résumé roulant et la fenêtre verbatim : l'état les porte
  (`Summary`, `Recent`), la stratégie de recompression basse température est un
  concern de service (appel modèle), hors du moteur pur.
- Le rendu client.

## Alternatives écartées

- **Étendre `ScenarioDocument` avec une capacité dirigée** : risque de dérive de hash
  sur tous les snapshots existants, et confusion de forme pour tout consommateur.
- **Porter les prédicats comme expressions exécutables** : viole l'invariant 8.
- **Exécuter le tour dans `Play`** : pollue le reducer déterministe.
- **Rendre un scénario dirigé jouable hors ligne par un narrateur de repli scripté** :
  ce serait un second moteur déterministe déguisé, sans la valeur du type, et une
  promesse trompeuse.
