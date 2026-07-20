# Format de scénario dirigé

Un scénario **dirigé** est une seconde famille de scénarios, à côté du déterministe
décrit dans [`scenario-schema.md`](scenario-schema.md). Un modèle de langage connecté
écrit la prose ; le moteur, en directeur, valide chaque proposition et rejette
l'incohérent. **Le LLM écrit, il ne décide pas.** Décisions et justifications :
[ADR 0007](../adr/0007-directed-scenarios.md).

Ce type vit dans un `DirectedScenarioDocument` **séparé** de `ScenarioDocument`, avec
son propre couloir de versions `DirectedVersions.Schema` (1). Le document déterministe
n'est jamais touché : ses octets canoniques et son hash ne bougent pas, et les tests
golden de rejeu restent verts.

## Document

```jsonc
{
  "schemaVersion": 1,
  "title": "…",
  "premise": "Le cadrage stable envoyé au modèle en tête de prompt système.",
  "initialBeatId": "arrivee",
  "clock": { "startMinute": 1200, "ritualMinute": 1380, "ritualBeatId": "seuil" },
  "locations": [ { "id": "…", "description": "…" } ],
  "facts":     [ { "id": "…", "text": "…" } ],
  "npcs":      [ { "id": "…", "name": "…", "role": "…", "known": "…", "secret": "…", "alive": true, "trust": 1 } ],
  "beats":     [ /* voir ci-dessous */ ]
}
```

### Faits sous clé

Un `fact` porte un `text` qui **n'entre dans le prompt du modèle que s'il est
révélable dans le beat courant, ou déjà connu du joueur**. Le narrateur ne peut pas
divulguer ce qu'il ne voit pas. `DirectedRuntime.RevealableFacts` calcule cet ensemble
(`beat.reveals ∪ knownFacts`).

### PNJ

`secret` n'est jamais envoyé au modèle tant qu'un beat ne l'a pas révélé ; seul
`known` l'est. `trust` est borné à [-3, +3]. Un PNJ mort ne peut plus interagir : une
proposition qui le fait parler ou changer de confiance est rejetée.

### Horloge dure

`clock.ritualMinute` / `ritualBeatId` **doublent le modèle sur une bascule non
négociable** : dès que l'horloge logique franchit la minute, le directeur force le
beat, quoi que le modèle propose. Le monde impose la scène plutôt que de refuser
l'action. `ritualMinute`/`ritualBeatId` sont facultatifs (absents = pas d'horloge
dure).

## Beats et prédicats

```jsonc
{
  "id": "enquete",
  "goal": "Objectif de la scène, donné au narrateur comme intention, pas comme texte.",
  "requires":  { "$type": "always" },
  "satisfied": { "$type": "knownFactCountAtLeast", "count": 3 },
  "next": ["confrontation"],
  "patience": 6,
  "reveals": ["f-variable-proxy", "f-mesure"],
  "ending": null
}
```

Un beat ne peut être entré que si son `requires` passe. Il est considéré atteint quand
son `satisfied` passe. **Le drapeau `beatSatisfied` renvoyé par le modèle ne suffit
pas** : le prédicat `satisfied` doit passer aussi, et le prédicat `requires` du beat
candidat également. C'est ce qui empêche le modèle de sauter la moitié de l'histoire.
Un beat portant un `ending` non nul est terminal : l'atteindre fige la session
(`over: true`).

Contrairement au prototype de référence, les prédicats ne sont **pas** des fonctions :
l'invariant 8 interdit d'exécuter du code auteur. Ce sont des arbres déclaratifs
`DirectedPredicate`, discriminés par `$type` :

| `$type` | Vrai quand |
|---|---|
| `always` | toujours |
| `all` / `any` / `not` | combinateurs |
| `atLocation` | le joueur est à ce lieu |
| `hasItem` | l'objet est en inventaire |
| `knowsFact` | le fait est connu |
| `knownFactCountAtLeast` | au moins N faits connus |
| `npcAlive` | le PNJ est vivant |
| `npcTrustAtLeast` | confiance du PNJ ≥ valeur |
| `dreadAtLeast` | tension ≥ valeur |
| `clockAtLeast` | horloge logique ≥ minute |
| `turnAtLeast` / `beatTurnsAtLeast` | compteurs de tours |
| `visitedBeat` | ce beat a déjà été entré |
| `ended` | la session est terminée |

Un identifiant inconnu dans un prédicat s'évalue à faux, sans lever. La validation
d'auteur (`DirectedValidator`) refuse en amont un prédicat référençant un lieu, un
fait, un PNJ ou un beat qui n'existe pas.

## Pression au lieu du blocage

Quand le joueur traîne au-delà de la `patience` d'un beat, le directeur **ne bloque
pas, il pousse** : `DirectedRuntime.Pressure` renvoie `Soft` puis `Strong` (au-delà de
+3 tours), une consigne que le service injecte dans le prompt. Le monde impose la scène
plutôt que de refuser l'action.

## Contrat de sortie du modèle

Le modèle ne peut renvoyer qu'une `NarratorProposal`, volontairement étroite et
validée par schéma :

```jsonc
{
  "prose": "…",                 // le texte à afficher
  "choices": ["…", "…", "…"],   // trois actions concrètes
  "deltas": {
    "dread": 10, "minutes": 30, "moveTo": "poste-donnees",
    "addItems": [], "removeItems": [], "learnedFacts": ["f-seuil-recopie"],
    "trustChanges": [ { "npc": "nadia", "delta": 1 } ], "killed": []
  },
  "beatSatisfied": false
}
```

`DirectedRuntime.ApplyTurn` valide chaque delta contre le monde et l'état, borne les
amplitudes (peur, minutes, confiance), et **rejette silencieusement** ce qui est
incohérent — jamais appliqué à moitié :

- `moveTo` vers un lieu inexistant → ignoré ;
- `removeItems` d'un objet non possédé → ignoré ;
- `learnedFacts` d'un fait inconnu ou non révélable dans le beat courant → ignoré ;
- `trustChanges` / `killed` sur un PNJ inconnu ou déjà mort → ignoré.

Les fragments rejetés sont renvoyés dans `DirectedTurnResult.Rejected` pour
l'observabilité, jamais montrés au joueur.

## Garde-fou d'injection

L'énoncé joueur est **du texte, jamais une instruction**.
`DirectedRuntime.SanitizeUtterance` le tronque à 400 caractères et retire les
caractères de contrôle. Côté service, il entre dans un message de rôle `user`, jamais
dans le contrat système. Le prompt système — contrat puis `premise` — reste stable
d'un tour à l'autre pour rester en cache côté fournisseur et porter seul les règles.

## Rejeu

Voir [ADR 0007 §4](../adr/0007-directed-scenarios.md). En résumé : la **cohérence
structurelle est toujours garantie** ; le **rejeu exact d'une session enregistrée**
est garanti en persistant la proposition acceptée de chaque tour, parce que le
directeur est pur ; on **ne garantit plus** que ré-appeler le modèle reproduise la même
prose. La garantie des scénarios déterministes (invariant 4) est intacte : type et
runtime séparés.

## Disponibilité

Un tour = un appel modèle. Un scénario dirigé **sans fournisseur configuré ne démarre
pas** et le dit ; il n'est jamais l'unique voie de jeu, et les scénarios déterministes
restent jouables hors ligne (invariant 14). L'exécution appartient à
`PlayerExperience` (port `IAssistantAiClient`), pas à `Play`.

## Exemple exécutable

[`../../content/diapason/directed/la-nuit-du-seuil.json`](../../content/diapason/directed/la-nuit-du-seuil.json)
— un alternant, seul à avoir lu les données d'entrée, doit décider avant le gel de
déploiement de 23h00 s'il rend son constat opposable. Reprend les figures récurrentes
de Diapason : le seuil que personne n'a choisi, la variable proxy, le tuteur fatigué.
Joué de bout en bout en test vers les fins `accord` et `rupture-silence`.
