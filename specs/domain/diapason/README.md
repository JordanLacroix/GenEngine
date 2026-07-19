# Le Diapason — bible d'univers

Configuration de référence de GenEngine. Ce dossier décrit **le contenu**, pas le moteur.

| Document | Objet |
|---|---|
| `README.md` | prémisse, ton, enjeux, position du joueur, figures récurrentes, grammaire visuelle et sonore |
| `taxonomy.md` | les six postures, leur justification et la règle d'affectation d'un scénario |
| `scenarios.md` | les dix scénarios, leur pitch, leur posture et leurs fins de rupture |
| `daily-rotation.md` | la sélection quotidienne déterministe |
| `seeding.md` | installation sur une instance neuve et industrialisation par client |

Les fichiers jouables sont sous [`content/diapason/`](../../../content/diapason/).

## Ce que Diapason est, et ce qu'il n'est pas

GenEngine est le moteur ; il se vend à des écoles et à des entreprises qui y injectent leur propre contenu. Diapason est la configuration avec laquelle on développe le moteur, avec laquelle on le démontre, et que toute instance neuve installe à sa première initialisation. Ce n'est pas « le jeu de GenEngine » : c'est la référence exécutable qui prouve que le moteur tient, et le gabarit qu'un client duplique pour écrire le sien.

Diapason remplace l'ancien contenu de démonstration (« Les braises sous la brume », « Le dernier phare »), qui n'existait que sous forme de slug non résolu et de libellés de thème.

## Prémisse

2026\. Notre monde, sans surcouche de fiction. Les systèmes d'intelligence artificielle sont déployés partout : dans les écoles, dans les administrations, dans les entreprises, dans les chaînes de décision qui accordent ou refusent un droit. Certains gouvernements en interdisent l'usage par peur, d'autres s'en servent pour nier que certaines personnes existent. L'accélération n'est maîtrisée par personne, y compris par ceux qui la produisent.

Le symptôme central n'est pas technique. C'est que les gens ont cessé de prendre du recul. Non par bêtise : parce que le recul coûte du temps, qu'il expose celui qui le prend, et qu'une réponse fluide, complète et immédiatement disponible désactive presque toujours l'envie de vérifier. La fluidité s'est substituée à la preuve.

Diapason ne raconte pas la révolte contre les machines. Il raconte la difficulté ordinaire, professionnelle et datée, de continuer à juger.

## Le joueur

Le joueur est **étudiant d'une école d'ingénieurs, en alternance**. Aucune école réelle n'est nommée, imitée ni reconnaissable. Ce statut n'est pas décoratif : il est la mécanique du jeu.

Un alternant est la seule personne d'une organisation qui cumule quatre propriétés utiles à ce récit :

- il est en position d'observer sans être responsable, ce qui rend le silence facile et donc coûteux ;
- il a régulièrement une information de terrain que personne au-dessus de lui ne possède ;
- il n'a aucune autorité, donc il ne peut peser que par des faits vérifiables ;
- il apprend, ce qui rend légitime qu'il se trompe et qu'il recommence.

Le joueur est le protagoniste. Il n'assiste jamais à une scène : il décide dedans, sous contrainte de temps, avec une information incomplète et un coût réel attaché à chaque option. Aucun scénario ne propose de « bonne réponse » à cocher.

## Ton

Prose française sobre, adulte, précise. Pas de vocabulaire d'entreprise, pas de pédagogie explicite, pas d'ironie facile sur la technologie. Les scènes sont horodatées, situées, matérielles : une salle, une heure, un nombre de dossiers, une échéance. Un scénario doit se lire comme une nouvelle courte dont le lecteur est l'acteur.

Trois interdits d'écriture :

1. **Aucun personnage ne fait la leçon.** L'enseignement passe par la conséquence, jamais par un mentor qui explique la morale.
2. **Aucun antagoniste technologique.** Les systèmes ne sont ni malveillants ni bêtes ; ils sont exacts sur des données incomplètes, et c'est précisément ce qui les rend dangereux.
3. **Aucun adversaire humain caricatural.** Les responsables sont pressés, fatigués et de bonne foi. Le tuteur qui dit « je sais » depuis onze mois n'est pas un cynique.

## Enjeux

Chaque scénario est construit sur un même squelette : le joueur détient, seul, une information ou une compétence ; l'utiliser coûte quelque chose ; ne pas l'utiliser coûte à quelqu'un d'autre, souvent hors champ. Le jeu ne récompense pas la lucidité en soi, il récompense la lucidité **rendue opposable** — un fait horodaté, une mesure, un test, une chaîne de provenance.

Les tensions traitées sont celles de juillet 2026 : réglementation et interdictions de modèles, effacement administratif de personnes, automatisation du jugement, provenance et authenticité des documents, dépendance et perte de compétence, déqualification silencieuse au travail.

## Figures récurrentes

- **Le document sans auteur.** Un texte parfaitement écrit que personne ne revendique et que la chaîne humaine traite comme s'il engageait quelqu'un.
- **Le seuil que personne n'a choisi.** Une valeur par défaut recopiée d'une documentation, devenue une décision politique sans décideur.
- **La variable proxy.** Trois critères licites qui, additionnés, reconstituent un critère interdit.
- **Le consensus.** Neuf personnes qui ont vérifié un raisonnement et aucune qui a vérifié ses entrées.
- **Le geste délégué une fois de trop.** La compétence qu'on croit posséder parce qu'on en a signé le résultat.
- **Le tuteur fatigué.** Celui qui sait déjà, qui a raison de vouloir bien faire, et qui n'a pas le temps.

Il n'existe pas de collectif clandestin, pas d'IA nommée et personnifiée, pas d'assistant-personnage héroïque. Le familier de la plateforme reste un outil de la plateforme, pas un protagoniste de Diapason.

## Fins et rejeu

Le moteur ne connaît qu'un seul concept terminal : `isEnding`. Diapason en dérive trois usages, distingués par convention de nommage :

| Préfixe de nœud | Sens |
|---|---|
| `fin-accord` | le joueur a atteint la posture visée et sait pourquoi |
| `fin-partielle` | le résultat est bon, le raisonnement n'est pas consolidé ; le texte invite explicitement à rejouer |
| `fin-rupture-*` | échec : la situation ne peut plus être rattrapée, le texte nomme la conséquence et renvoie au point de départ |

Une fin de rupture n'est pas une punition et ne comporte aucun jugement sur le joueur : elle décrit ce qui s'est produit dans le monde et rend la reprise nécessaire. Le moteur n'expose aucun drapeau de type « game over » ; l'obligation de recommencer est portée par le texte et par le fait que la partie est `Completed`. Voir la limite signalée dans `scenarios.md`.

## Fin de partie globale

Au-dessus des fins internes aux scénarios, Diapason déclare une **fin de jeu** dans sa configuration de référence : « Ce qui reste après vous ». Elle se déclenche en mode `All`, sur deux conditions — avoir terminé la posture « Autonomie », et avoir terminé au moins huit des dix scénarios.

La condition vise une **catégorie** plutôt que le parcours homonyme, alors que « Ce qui reste après toi » se lirait mieux. C'est délibéré : les parcours sont faits pour être recomposés par chaque client au-dessus des six postures, et y épingler la fin livrée par défaut rendrait invalide toute réécriture des parcours. Les six postures sont l'axe stable de Diapason ; les parcours sont une composition. Une configuration cliente qui fige ses propres parcours peut, elle, utiliser `JourneyCompleted`.

Le texte de clôture assume la position du jeu : *« Vous n'avez pas gagné, et vous n'avez rien perdu non plus. »* Il énonce aussi la règle du moteur, qui est ici une règle de récit : **atteindre la fin ne ferme rien**. Les scénarios déjà joués gardent leurs branches non ouvertes, et le joueur y revient s'il le souhaite. La fin est un accord tenu, pas une porte.

## Le familier dans Diapason

Le familier de la plateforme reste un outil, pas un protagoniste — mais son apparence est désormais choisie par le joueur sur neuf axes catalogués. La configuration de référence propose la forme `tuning-fork` par défaut, ce qui accorde l'outil au titre : un objet sobre qui vibre quand quelque chose sonne faux. Les formes `spark`, `echo`, `owl` et `fox` restent offertes, ainsi que les couleurs de la palette ci-dessous, de sorte qu'un familier personnalisé ne sort jamais de la direction artistique.

## Grammaire visuelle et sonore

Diapason reprend intégralement la direction artistique et sonore du POC « Le Diapason », dont ce récit est la version rapportée au monde réel.

**Palette** — encre `#17344a`, ivoire `#fffaf0`, sauge `#7a9a55`, or `#d7a746`, azur `#2f7fa0`, contour doux `#c8b98d`. Illustration 2D peinte, contours doux, caméra à hauteur humaine, lumière venant du haut-gauche.

**Quatre actes** — chaque scénario déclare son acte dans le manifeste ; l'acte pilote la lumière, la palette dérivée et la piste musicale.

| Acte | Couleur dominante | Tempo | Intention |
|---|---|---:|---|
| Avènement | or et cyan | 72 BPM | la séduction du fluide et de l'immédiat |
| Extinction | bleu ardoise et violet | 58 BPM | le doute qui s'éteint, le malaise discret |
| Résistance | cuivre et ambre | 66 BPM | le travail lent, l'attention reprise |
| Observatoire | indigo et lumière d'aube | 64 BPM | le jugement réaccordé, la responsabilité |

**Son** — trois couches indépendantes : ambiance environnementale en boucle, piste instrumentale par acte entre 58 et 72 BPM sans percussion insistante, signatures courtes pour choix, erreurs et récompenses. Le motif du diapason associe `La 440 Hz` et `Mi 659,25 Hz` ; il signale que le joueur a rendu sa démarche explicite, jamais qu'il a « bien répondu ». Tout le son est désactivable et ne porte aucune information exclusive.

**Récompenses** — ce sont des fréquences, jamais des classements : `frequence-du-doute` (avoir suspendu une conclusion trop fluide), `frequence-des-biais` (avoir identifié ce qu'un système mesure réellement et qui il désavantage).

La production des assets visuels et sonores relève d'un autre chantier ; ce dossier ne livre que le contenu narratif et la configuration.
