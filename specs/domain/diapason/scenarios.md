# Les dix scénarios

Fichiers jouables : [`content/diapason/scenarios/`](../../../content/diapason/scenarios/). Les documents sont en `schemaVersion` 2, sauf les trois qui présentent un document au joueur (`la-note-de-service`, `la-revue-automatique`, `le-tri-des-candidatures`), déclarés en `schemaVersion` 6. Tous sont validés par `ScenarioValidator` dans `GenEngine.Narrative.Tests.DiapasonContentTests`.

## Documents présentés au joueur

Trois scénarios n'évoquent plus seulement leur pièce centrale : ils la montrent, via l'interaction `document` du schéma v6 (voir [`../scenario-schema.md`](../scenario-schema.md)). Les trois natures sont volontairement différentes, ce qui vérifie que le modèle tient hors d'un seul exemple :

| Scénario | Interaction | Nature | Échantillon |
|---|---|---|---|
| La note de service | `la-note` | `Memo` | 4 paragraphes sur 27 |
| La revue automatique | `le-diff` | `Diff` | aucun — le correctif est montré en entier |
| Le tri des candidatures | `le-classement` | `Table` | 6 rangées sur 412 |

Chacun est `isOptional: true` : **consulter n'est jamais obligatoire**. Chacun débloque en revanche un choix conditionné par `consultedDocument`, absent pour qui n'a pas lu — respectivement relever que « validée collégialement » ne nomme personne, opposer la règle CONC-014 au code réel, et interroger le motif qui classe le 380e. C'est la lecture, pas l'intuition, qui ouvre la sortie lucide.

## Parcours 1 — Le premier accord

### 1. La note de service — *Lucidité*, acte Avènement

Une note de réorganisation parfaitement rédigée circule dans le service ; elle a été produite à 6 h 47 par un compte applicatif à partir d'un brouillon à quatre hypothèses, et le mot « validée » n'existe pas dans la source. La relayer la rend vraie.

Ruptures : le silence après diffusion ; l'excuse sans réparation ; l'inertie devant une conséquence visible ; l'intuition juste sans chaîne de provenance.

### 2. Le dossier sans auteur — *Lucidité*, acte Extinction

Un cahier des charges de quarante-deux pages, généré en quatre minutes, sans un seul blanc — et deux règles de facturation incompatibles écrites avec la même assurance. Un document généré n'a pas de trous, ce qui est exactement ce qui le rend dangereux.

Ruptures : trancher seul les onze points non décidés ; alerter deux heures avant la recette ; laisser le client arbitrer en public.

### 3. Identité non reconnue — *Discernement*, acte Extinction

Une candidate est refusée par le service de vérification d'identité : prénom d'usage, registre national inaccessible, photo floue. Le refus n'est pas une décision, c'est un score de 0,71 sous un seuil de 0,85 recopié d'une documentation d'intégration par quelqu'un qui n'est plus là.

Ruptures : le renvoi vers un recours qu'on sait trop lent ; la ligne écrite en base sans autorité ; le seuil abaissé par empathie ; le cas isolé jamais relié à sa population.

### 4. Le modèle interdit — *Discernement*, acte Résistance

Vendredi 17 h 55, une décision d'exécution interdit à compter de lundi la catégorie d'usage sur laquelle repose le produit. Quatre-vingt-onze heures. La réponse d'ingénieur — changer de fournisseur — arrive vite et rate la cible : l'interdiction porte sur ce que le produit prétend faire.

Ruptures : l'esquive hiérarchique ; l'erreur non corrigée devenue décision ; le pari sur un référé ; la bascule impeccable livrée sur un produit toujours interdit.

## Parcours 2 — La chaîne de décision

### 5. La revue automatique — *Arbitrage*, acte Extinction

La revue de code automatique bloque un correctif de deux lignes avec un verdict cohérent, détaillé, référencé — et faux. Mise en production à 22 h. Obéir produit une panne pire ; contourner laisse le défaut en place ; la seule sortie coûte cinquante minutes de démonstration.

Ruptures : le verrou appliqué sans comprendre ; la responsabilité transférée à l'outil ; la dérogation vidée de sa substance ; la preuve utilisée pour s'exempter seul.

### 6. Le tri des candidatures — *Arbitrage*, acte Observatoire

Quatre cent douze dossiers, douze places, cinq jours, un outil de classement. Il place onze des douze recrutés de l'an dernier dans son premier quart, et classe 380e la meilleure recrue des cinq dernières années. Aucune variable interdite n'est utilisée ; trois variables licites suffisent à reconstituer une origine sociale.

Ruptures : le tri manuel épuisé qui devient un biais non mesurable ; le paragraphe d'alerte joint à la livraison de ce qu'on dénonce ; le refus sans méthode de remplacement ; la position juste abandonnée faute de mesure ; la responsabilité renvoyée à l'éditeur.

### 7. La réunion où personne ne doute — *Courage*, acte Avènement

Neuf cadres alignés sur une recommandation produite à partir de quatre-vingts documents internes. Le point 3 parle de quatre entrepôts ; il y en a cinq depuis janvier, et le joueur est la seule personne du bâtiment à avoir câblé les intégrations. Le scénario se joue entièrement sur la formulation de la première phrase.

Ruptures : le silence justifié par l'expérience supposée des autres ; l'attachement à sa formulation plutôt qu'à son effet ; le courage sous-traité à sa tutrice.

### 8. Le signalement — *Courage*, acte Résistance

Une table non documentée enregistre depuis onze mois la frappe au clavier de trois cent mille élèves et alimente un score « élèves à accompagner ». Signaler n'est ni dénoncer ni une affaire de tempérament : c'est établir un écart vérifiable, ne rien exfiltrer, et choisir un destinataire qui a l'obligation d'instruire.

Ruptures : la publication immédiate qui expose les personnes qu'on protège ; l'onglet fermé ; « j'ai fait ma part » après un seul interlocuteur sans obligation ; l'escalade externe prématurée ; le signalement de mémoire traité comme une rumeur.

## Parcours 3 — Ce qui reste après toi

### 9. La spécification avant le code — *Transmission*, acte Résistance — **Spec Driven Development**

Une demande de trois phrases, dix jours, et une implémentation complète proposée avant la fin de la lecture — avec une constante `REFUND_WINDOW_HOURS = 24` que personne n'a décidée et que le métier fixe à quarante-huit. Le joueur écrit neuf affirmations vérifiables, les fait signer, les transforme en tests, puis laisse l'assistant proposer l'implémentation : la suite de tests devient l'autorité. Trois règles métier échouent — exactement les trois qu'aucun modèle ne pouvait deviner.

Ce scénario utilise une interaction `freeText` : le joueur doit identifier lui-même le cas limite absent de sa propre spécification.

Ruptures : la rationalisation après coup d'une valeur issue d'une complétion ; la spécification validée que rien n'exécute ; la validation repoussée jusqu'à la recette.

### 10. La compétence qui s'efface — *Autonomie*, acte Observatoire

Panne réseau, donc pas d'assistant, et un module de facturation qui produit des montants faux à quatre heures de la clôture. Le joueur a écrit ce module en janvier. Il le regarde quatre minutes et comprend qu'il sait tout de lui sauf comment il marche.

Ruptures : la compréhension chèrement acquise et non transmise ; le contournement automatisé qui rend l'opacité permanente.

## Ce que le schéma actuel ne permet pas d'exprimer

Constats issus de l'écriture, à traiter hors de ce lot de contenu.

1. **Aucun drapeau d'échec de première classe.** Le moteur ne connaît que `isEnding`. Une fin de rupture et une fin de réussite sont indiscernables pour le runtime, les projections joueur et l'économie : `PlayerProjectionBuilder` et les règles de récompense ne peuvent pas distinguer une partie terminée d'une partie perdue. Diapason contourne par une convention de nommage (`fin-rupture-*`) et par le texte, ce qui n'est pas opposable côté service. Un champ `outcome` sur le nœud terminal serait le correctif minimal.
2. **Aucun métadonnée d'auteur sur le document.** Le `ScenarioDocument` porte `schemaVersion`, `title`, `initialNodeId` et les nœuds. Il ne porte ni slug, ni catégorie, ni parcours, ni durée estimée, ni acte. Ces informations vivent dans `content/diapason/manifest.json` et sont recopiées à l'import ; rien ne garantit leur cohérence côté moteur.
3. **Pas de prérequis au niveau scénario.** Les prérequis existent au niveau parcours (`PrerequisiteJourneyIds`). Voir `daily-rotation.md` pour l'analyse du besoin réel.
4. **Pas de rotation quotidienne dans le modèle de configuration.** Voir `daily-rotation.md`.
5. **`recordNotableEvent` porte un `scope` libre.** Diapason y met `learning`, `consequence`, `rupture`, et le nom d'une posture. Aucun de ces scopes n'est déclaré ni validé côté configuration ; deux configurations clientes peuvent diverger silencieusement.
