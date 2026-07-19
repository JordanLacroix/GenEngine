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

Le familier joueur accepte une définition publiée, une image HTTPS, un nom personnel, un niveau d'aide, une fréquence, une préférence proactive et un **ensemble d'axes de personnalisation catalogués**. Le resolver contextuel actuel est hors ligne : indices auteur, avertissement de branche connue, puis message déterministe par contexte. Le futur adaptateur IA ne devra jamais bloquer le jeu et conservera ce fallback.

### Axes de personnalisation

Neuf axes sont livrés : `form`, `tone`, `writingStyle`, `accent` (couleur), `aura`, `silhouette`, `speechRhythm`, `languageRegister` et `interventionDensity`. **Aucun n'accepte de texte libre.** Chaque option porte une valeur stable, un libellé, la description de son effet, un jeton d'accent facultatif et une référence d'asset facultative — c'est ce qui la rend prévisualisable, et c'est précisément ce que `writingStyle` et `accent` ne permettaient pas tant qu'ils étaient libres.

La compatibilité est assurée dans les deux sens :

- une configuration antérieure, qui ne déclare que `availableForms` et `availableTones`, voit son catalogue **dérivé** à la publication, en conservant comme options les valeurs qu'elle utilisait déjà ; `availableForms` et `availableTones` restent servis, désormais déduits des axes `form` et `tone`, pour les clients non encore mis à jour ;
- un profil antérieur reste lisible : les quatre colonnes historiques sont conservées et alimentées depuis la carte d'axes, et un axe jamais choisi retombe sur le `defaultValue` de l'axe ;
- l'ajout d'un axe ne demande aucune migration : les sélections sont stockées dans une seule colonne `jsonb`.

Le nom personnel reste libre, mais borné à 80 caractères imprimables et refusant `<`, `>`, `&` et les caractères de contrôle : il est rendu par deux clients dont on ne peut pas auditer chaque moteur de rendu, donc il est refusé plutôt qu'échappé.

## Scénario de fin

La configuration peut déclarer un bloc `finale` facultatif : un titre, un texte de clôture et des conditions composables en `All` ou `Any`. Les conditions s'évaluent de façon déterministe à partir de `ScenarioMastery`, la maîtrise cross-session déjà enregistrée par (profil, version de scénario) ; aucun second système de suivi n'est introduit.

`GET /me/experience` expose la progression vers la fin condition par condition (`current`, `target`, `satisfied`), donc un joueur voit ce qu'il lui reste plutôt qu'une porte fermée.

**Atteindre la fin ne verrouille rien.** C'est un seuil franchi et mémorisé : `finaleId` et `finaleReachedAt` sont estampillés une seule fois, une entrée de journal `FinaleReached` est écrite une seule fois, et le joueur continue ensuite de jouer, de progresser et d'être récompensé sans aucune différence. Le modèle ne comporte volontairement aucun drapeau permettant de rendre la fin bloquante.

## Permissions

Le rôle Player reçoit explicitement `assistant.use`, `assistant.customize`, `onboarding.use`, `onboarding.reset.own`, `progress.read.own`, `journal.read.own`, `journal.export.own`, `help.read` et `media.read`. Les nouveaux droits sont synchronisés dans les rôles système existants lors de la migration de la base Identity.

## Limites encore ouvertes

- service Assistant avec routage Azure AI Foundry, quotas et metering ;
- stockage Media géré au lieu de simples URL HTTPS ;
- affectation parcours/catégories avec déblocages avancés ;
- back-office complet pour éditer tous les nouveaux objets sans JSON ;
- export effectif du journal et supervision de progression multi-utilisateur.
