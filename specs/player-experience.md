# Expérience joueur immersive

## Flux autoritatif

Le client charge d'abord la configuration publique puis, après authentification, `GET /me/experience/bootstrap`. La propriété `nextAction` impose l'ordre : `ConfigureFamiliar`, `ResumeOnboarding` ou `OpenMap`. Les clients ne recréent pas cette règle.

L'introduction est publique, skippable selon configuration et versionnée. Le tutoriel est un état serveur (`NotStarted`, `InProgress`, `Completed`, `Skipped`) identifié par tutoriel et version. Les commandes de complétion et de passage sont idempotentes.

## Progression et journal

Après chaque commande narrative acceptée, `Play` envoie un événement interne idempotent à `PlayerExperience`. Celui-ci conserve :

- une chronologie personnelle exploitable par type, parcours, catégorie ou scénario ;
- les choix, nœuds, fins et sessions déjà explorés ;
- un pourcentage de maîtrise dérivé des objectifs découvrables de la version publiée.

Le snapshot narratif reste la source de vérité d'une session. La progression cross-session est une projection utilisateur et ne modifie jamais le reducer déterministe.

## Parcours par défaut et progression

Le joueur choisit un parcours par défaut (`PUT /me/experience/journey`) et peut en changer à tout moment ; la carte s'actualise sans autre appel puisque `GET /me/experience` et `GET /me/experience/bootstrap` portent déjà `effectiveJourney`. Le choix est validé contre le document publié : un parcours inexistant, invisible ou encore verrouillé est refusé. Le parcours **effectif** est le parcours choisi tant qu'il reste visible et déverrouillé, sinon le premier parcours visible et déverrouillé dans l'ordre publié — un joueur dont le parcours a disparu d'une nouvelle publication ouvre donc quand même une carte utilisable.

La progression est **agrégée**, jamais recalculée : `ScenarioMastery` reste la source de vérité. Pour un parcours comme pour une catégorie, le service expose le nombre de scénarios, le nombre entamés, le nombre terminés et un pourcentage. Un scénario est entamé dès qu'une maîtrise existe, et le pourcentage est la moyenne des maîtrises sur tous les scénarios de la portée, les scénarios jamais joués comptant pour zéro.

`ScenarioMastery` étant clé par version publiée, un scénario joué sur plusieurs versions est replié sur **deux critères indépendants** : le pourcentage retient la meilleure version atteinte, tandis que « terminé » est un OU logique sur toutes les versions. Lire l'achèvement sur la seule version la mieux maîtrisée dé-terminerait un scénario dès que le joueur explore une version plus récente sans aller au bout — la curiosité verrouillerait les parcours en aval, exactement l'inverse de l'intention.

## Assistant et aide

Le familier joueur accepte une définition publiée, une image HTTPS, un nom personnel, une forme, un ton, un style, un niveau d'aide, une fréquence et une préférence proactive. Le resolver contextuel actuel est hors ligne : indices auteur, avertissement de branche connue, puis message déterministe par contexte. Le futur adaptateur IA ne devra jamais bloquer le jeu et conservera ce fallback.

## Permissions

Le rôle Player reçoit explicitement `assistant.use`, `assistant.customize`, `onboarding.use`, `onboarding.reset.own`, `progress.read.own`, `journal.read.own`, `journal.export.own`, `help.read`, `media.read` et `journey.read`. `journey.read` couvre la lecture des parcours **et** le choix de son propre parcours par défaut : choisir pour soi est une préférence personnelle, pas une autorité sur le catalogue, laquelle relève de `journey.manage` côté `Configuration`. Les nouveaux droits sont synchronisés dans les rôles système existants lors de la migration de la base Identity.

## Limites encore ouvertes

- service Assistant avec routage Azure AI Foundry, quotas et metering ;
- stockage Media géré au lieu de simples URL HTTPS ;
- règles de déblocage avancées : seuls les prérequis directs de parcours sont évalués, sans seuil partiel, sans fenêtre temporelle ni condition de rôle ;
- back-office complet pour éditer tous les nouveaux objets sans JSON ;
- export effectif du journal et supervision de progression multi-utilisateur.
