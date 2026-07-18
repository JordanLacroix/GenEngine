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

## Assistant et aide

Le familier joueur accepte une définition publiée, une image HTTPS, un nom personnel, une forme, un ton, un style, un niveau d'aide, une fréquence et une préférence proactive. Le resolver contextuel actuel est hors ligne : indices auteur, avertissement de branche connue, puis message déterministe par contexte. Le futur adaptateur IA ne devra jamais bloquer le jeu et conservera ce fallback.

## Permissions

Le rôle Player reçoit explicitement `assistant.use`, `assistant.customize`, `onboarding.use`, `onboarding.reset.own`, `progress.read.own`, `journal.read.own`, `journal.export.own`, `help.read` et `media.read`. Les nouveaux droits sont synchronisés dans les rôles système existants lors de la migration de la base Identity.

## Limites encore ouvertes

- service Assistant avec routage Azure AI Foundry, quotas et metering ;
- stockage Media géré au lieu de simples URL HTTPS ;
- affectation parcours/catégories avec déblocages avancés ;
- back-office complet pour éditer tous les nouveaux objets sans JSON ;
- export effectif du journal et supervision de progression multi-utilisateur.
