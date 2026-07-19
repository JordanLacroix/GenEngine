# Taxonomie par posture

## Pourquoi pas des matières

Une catégorie « gestion de projet », « management » ou « cybersécurité » classe le décor d'un scénario, pas ce qu'il exerce. Deux conséquences pratiques :

- un même geste — refuser une conclusion trop fluide — se disperse dans six matières et n'est jamais travaillé comme tel ;
- un client qui achète le moteur doit remapper toute la taxonomie sur son propre référentiel de compétences dès le premier jour.

Diapason classe donc par **posture** : ce que le joueur doit tenir face à la situation. Le décor devient libre. Dans une même posture, un scénario peut porter sur du management, de la gestion de projet, un problème technique, une résolution de conflit ou l'apprentissage d'une matière — c'est le cas dans les dix scénarios livrés.

Une posture est retenue si elle satisfait trois critères : elle décrit un geste observable et non une qualité de caractère ; elle peut échouer d'une manière reconnaissable et coûteuse ; elle reste vraie hors du contexte IA, ce qui garantit que le contenu ne périme pas avec une génération de modèles.

## Les six postures

### Lucidité

*Voir ce qui est réellement là avant d'interpréter.*

Séparer une affirmation de ce qui l'établit : qui a produit ce texte, à partir de quoi, quand, et qu'est-ce qui a été ajouté en route. La lucidité est la posture d'entrée parce que toutes les autres en dépendent — on ne peut ni arbitrer ni alerter sur une base qu'on n'a pas vérifiée.

Échec caractéristique : traiter comme vérifié ce qui est seulement bien écrit.

### Discernement

*Trier ce qui compte quand tout est plausible.*

Distinguer la qualité d'une preuve de sa présentation, identifier ce qu'un système mesure réellement, reconnaître qu'un refus automatique est un arbitrage déguisé en constat. Là où la lucidité demande « d'où cela vient-il », le discernement demande « qu'est-ce que cela pèse, et pour qui ».

Échec caractéristique : accepter une catégorie fournie par le système au lieu de la sienne.

### Arbitrage

*Décider sous contrainte et assumer ce qu'on perd.*

Toute décision réelle a un coût qu'on choisit de payer. La posture consiste à rendre ce coût explicite plutôt qu'à le laisser tomber par défaut sur l'absent — et à ne jamais refuser sans proposer de méthode de remplacement.

Échec caractéristique : le refus pur, ou l'obéissance à un blocage qu'on n'a pas su contester.

### Courage

*Parler, refuser ou signaler quand c'est coûteux.*

Le courage traité ici n'est pas un tempérament, c'est une procédure : choisir le moment, la forme et le destinataire. Une objection juste, mal formulée ou portée trop tard, ne produit rien — et déplace souvent son coût sur quelqu'un d'autre.

Échec caractéristique : le silence justifié par l'autorité supposée des autres ; l'alerte confiée à quelqu'un qui n'a pas d'obligation d'instruire.

### Transmission

*Rendre son raisonnement utilisable par d'autres.*

Écrire ce qui doit être vrai, sous une forme qu'une machine peut vérifier et qu'un humain doit signer. Cette posture prend une importance nouvelle quand l'implémentation devient instantanée : ce qui reste rare n'est plus le code, c'est la décision écrite que le code applique. C'est là que vit le Spec Driven Development.

Échec caractéristique : la compréhension gardée pour soi, ou le document validé que rien n'exécute.

### Autonomie

*Garder une compétence qu'on pourrait déléguer.*

Déléguer une tâche fait gagner du temps ; déléguer la compréhension fait perdre la capacité de reprendre la main, et cette perte est invisible jusqu'à ce qu'elle soit totale. La posture consiste à décider explicitement ce qu'on doit encore pouvoir faire seul, et à le vérifier périodiquement.

Échec caractéristique : signer un travail qu'on ne saurait pas refaire.

## Progression

Les six postures sont regroupées en trois parcours ordonnés, dont le déblocage s'exprime par `PrerequisiteJourneyIds` dans le service Configuration.

| Parcours | Postures | Prérequis |
|---|---|---|
| Le premier accord | Lucidité, Discernement | aucun |
| La chaîne de décision | Arbitrage, Courage | Le premier accord |
| Ce qui reste après toi | Transmission, Autonomie | La chaîne de décision |

L'ordre n'est pas une difficulté croissante, c'est une dépendance : on ne peut pas arbitrer sans avoir établi les faits, ni transmettre un raisonnement qu'on n'a pas su tenir sous pression.

## Règle d'affectation

Un scénario appartient à la posture **qui décide de sa fin**, pas à celle qu'il mobilise le plus souvent. Presque tous les scénarios de Diapason exercent la lucidité en chemin ; seuls deux se terminent sur elle. En cas d'hésitation, la question est : si le joueur échoue, quelle posture lui a manqué au moment décisif ?

Un scénario ne porte qu'une posture. Les gestes secondaires sont exprimés par les caractéristiques du moteur (`lucidite`, `discernement`, `arbitrage`, `courage`, `transmission`, `autonomie`), qui progressent de façon transverse et servent aux `characteristicGate`.
