# Les dix scénarios

Fichiers jouables : [`content/diapason/scenarios/`](../../../content/diapason/scenarios/). Les dix documents sont en `schemaVersion` 6. Tous sont validés par `ScenarioValidator` dans `GenEngine.Narrative.Tests.DiapasonContentTests`.

## Documents présentés au joueur

Aucun scénario ne se contente plus d'évoquer sa pièce centrale : chacun la montre, via l'interaction `document` du schéma v6 (voir [`../scenario-schema.md`](../scenario-schema.md)). Les natures sont volontairement variées, ce qui vérifie que le modèle tient hors d'un seul exemple :

| Scénario | Interaction | Nature | Échantillon |
|---|---|---|---|
| La note de service | `la-note` | `Memo` | 4 paragraphes sur 27 |
| Le dossier sans auteur | `le-cahier` | `Report` | 7 paragraphes sur 214 |
| Identité non reconnue | `le-journal` | `Log` | 9 entrées sur 1 348 |
| Le modèle interdit | `la-decision` | `Report` | 5 paragraphes sur 62 |
| La revue automatique | `le-diff` | `Diff` | aucun — le correctif est montré en entier |
| Le tri des candidatures | `le-classement` | `Table` | 6 rangées sur 412 |
| La réunion où personne ne doute | `le-point-3` | `Report` | 4 paragraphes sur 96 |
| Le signalement | `la-table` | `Code` | 11 lignes sur 34 |
| La spécification avant le code | `la-proposition` | `Code` | 12 lignes sur 87 |
| La compétence qui s'efface | `mon-module` | `Code` | 10 lignes sur 143 |

Chacun est `isOptional: true` : **consulter n'est jamais obligatoire**. Chacun débloque en revanche un choix conditionné par `consultedDocument`, absent pour qui n'a pas lu. C'est la lecture, pas l'intuition, qui ouvre la sortie lucide — vérifier coûte du temps et change ce qu'on peut dire.

| Scénario | Ce que la lecture ouvre |
|---|---|
| La note de service | relever que « validée collégialement » ne nomme personne |
| Le dossier sans auteur | citer 4.2 et 7.1 mot pour mot et demander laquelle engage le client |
| Identité non reconnue | opposer que le seuil de 0,85 est une valeur d'exemple du guide d'intégration |
| Le modèle interdit | lire l'article 2 : l'interdiction vise la finalité, pas le fournisseur |
| La revue automatique | opposer la règle CONC-014 au code réel |
| Le tri des candidatures | interroger le motif qui classe le 380e |
| La réunion où personne ne doute | citer le point 3 et demander la date d'indexation des sources |
| Le signalement | relever sept colonnes comportementales, aucune purge, zéro mention contractuelle |
| La spécification avant le code | nommer la constante `REFUND_WINDOW_HOURS` que personne n'a décidée |
| La compétence qui s'efface | suivre le diviseur constant de trente jours |

## Aide d'auteur et média

L'aide d'auteur du schéma v5 (`help`) est portée par les nœuds et les choix — **jamais par une interaction**, que le modèle n'accepte pas. Les quatre modalités sont employées selon leur sens : `objective` sur les choix décisifs, `hint` pour la piste sans la réponse, `consequence` sur les options coûteuses, et `blocker` sur chaque choix conditionné par `consultedDocument`, où il explique au joueur pourquoi l'option reste fermée.

Côté média, le pack `diapason-core` ne contient ni illustration ni ambiance (voir les manques déclarés dans [`asset-sourcing-plan.md`](asset-sourcing-plan.md)). Aucun `visualUrl` n'est donc renseigné : renvoyer vers une image inexistante serait une promesse fausse. Seul le son est employé, sur les cinquante-trois nœuds terminaux — `stinger.reward.primary` sur les fins d'accord et de réparation, `sfx.error.soft` sur les fins de rupture.

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

## La démonstration

Le bloc `demo` de la configuration continue de pointer sur **La note de service** (`demo.scenarioSlug`). Ce choix est délibéré et il n'a pas paru justifié d'écrire un scénario dédié : la note de service est déjà le premier scénario du premier parcours, en acte Avènement, conçu comme la porte d'entrée du contenu. Un scénario écrit exprès pour la démonstration aurait montré les mêmes capacités en cessant d'être une partie — c'est-à-dire en devenant le catalogue technique qu'on cherchait à éviter.

Elle porte désormais, sans que la mécanique prenne le pas sur le récit : une interaction `document` facultative dont la consultation ouvre un quatrième choix, un `quiz` sur la nature d'une affirmation, une interaction `freeText` à son climax — le joueur formule lui-même la phrase opposable au lieu de la choisir dans une liste —, l'aide d'auteur sur les nœuds et les choix, et le son sur ses six fins. Un visiteur anonyme y rencontre donc l'ensemble des capacités du moteur en jouant une nouvelle courte, pas en parcourant une démonstration.

Le `freeText` de clôture retient les termes `47`, `applicatif`, `brouillon`, `hypothese`, `validee` et `source`, avec `minimumMatches: 2` : plusieurs formulations honnêtes sont acceptées, une conviction sans fait ne l'est pas.

## Ce que le schéma actuel ne permet pas d'exprimer

Constats issus de l'écriture, à traiter hors de ce lot de contenu.

1. **Aucun drapeau d'échec de première classe.** Le moteur ne connaît que `isEnding`. Une fin de rupture et une fin de réussite sont indiscernables pour le runtime, les projections joueur et l'économie : `PlayerProjectionBuilder` et les règles de récompense ne peuvent pas distinguer une partie terminée d'une partie perdue. Diapason contourne par une convention de nommage (`fin-rupture-*`) et par le texte, ce qui n'est pas opposable côté service. Un champ `outcome` sur le nœud terminal serait le correctif minimal.
2. **Aucun métadonnée d'auteur sur le document.** Le `ScenarioDocument` porte `schemaVersion`, `title`, `initialNodeId` et les nœuds. Il ne porte ni slug, ni catégorie, ni parcours, ni durée estimée, ni acte. Ces informations vivent dans `content/diapason/manifest.json` et sont recopiées à l'import ; rien ne garantit leur cohérence côté moteur.
3. **Pas de prérequis au niveau scénario.** Les prérequis existent au niveau parcours (`PrerequisiteJourneyIds`). Voir `daily-rotation.md` pour l'analyse du besoin réel.
4. **Pas de rotation quotidienne dans le modèle de configuration.** Voir `daily-rotation.md`.
5. **`recordNotableEvent` porte un `scope` libre.** Diapason y met `learning`, `consequence`, `rupture`, et le nom d'une posture. Aucun de ces scopes n'est déclaré ni validé côté configuration ; deux configurations clientes peuvent diverger silencieusement.
