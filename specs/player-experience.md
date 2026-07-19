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

Le familier joueur accepte une définition publiée, une image HTTPS, un nom personnel, une forme, un ton, un style, un niveau d'aide, une fréquence et une préférence proactive.

### Résolution de l'aide contextuelle

L'aide est résolue **côté serveur** et paramétrable au niveau des scénarios. `POST /me/experience/assistant/contextual-help` applique cet ordre, chaque étape se dégradant silencieusement dans la suivante :

1. **Silence** — assistant désactivé, ou demande proactive alors que le familier n'est pas proactif ou que sa fréquence d'intervention est nulle. Renvoie `Suppressed` et un message vide. Une aide explicitement demandée reste servie : le silence porte sur la proactivité, pas sur le refus de répondre.
2. **`KnownPathWarning`** — chemin déjà exploré et `warnOnKnownPath` actif.
3. **`Ai`** — un fournisseur est configuré et la politique l'autorise.
4. **`AuthorHint`** — surcharge fournie par le client, conservée pour compatibilité.
5. **`ScenarioHelp`** — l'objet `help` porté par la version publiée, relu via la route interne d'Authoring. L'aide de choix l'emporte sur l'aide de nœud.
6. **`OfflineRule`** — message générique intégré. C'est la **seule** branche pour laquelle `isFallback` vaut vrai.

`source` désigne toujours la source du message réellement retourné, jamais une source seulement consultée.

Le niveau d'aide choisit la modalité servie : `0 → Objective`, `1-2 → Hint`, `3 → Consequence`, `4-5 → Blocker`, avec dégradation déterministe vers une autre modalité si l'auteur n'a rien écrit pour celle visée.

L'appel de `Play` vers `Authoring` étant déjà résilient, celui de `PlayerExperience` l'est aussi (timeouts, retry borné, circuit breaker, budgets plus courts). Authoring indisponible dégrade l'aide, ne la fait jamais échouer.

### Déterminisme et confidentialité

L'aide est une **surcouche de présentation** : l'appel est en lecture seule, ne modifie aucun état de session, ne consomme aucun tour, n'écrit ni journal ni portefeuille et n'entre dans aucun hash. Un test le vérifie.

L'IA reste optionnelle : absence de fournisseur, panne, erreur ou dépassement de délai retombent sur les règles hors ligne. Par défaut aucun fournisseur n'est enregistré.

Ce qui transite vers un fournisseur, et **rien d'autre** : le front, le contexte d'appel, la modalité visée, le niveau d'aide, le titre du scénario, l'identifiant et le texte du nœud courant, les libellés des choix visibles, l'aide d'auteur retenue, le drapeau « déjà exploré », puis le nom, le ton et le style d'écriture du familier. Ne transitent notamment **jamais** : identifiant utilisateur, portefeuille, journal, historique de progression et maîtrise.

Aucun secret n'atteint ce service : un fournisseur résout ses propres credentials localement, et le document de configuration publié que `PlayerExperience` lit voit son `secretReference` supprimé par `Configuration` avant d'être servi.

## Permissions

Le rôle Player reçoit explicitement `assistant.use`, `assistant.customize`, `onboarding.use`, `onboarding.reset.own`, `progress.read.own`, `journal.read.own`, `journal.export.own`, `help.read` et `media.read`. Les nouveaux droits sont synchronisés dans les rôles système existants lors de la migration de la base Identity.

## Limites encore ouvertes

- service Assistant avec routage Azure AI Foundry, quotas et metering ;
- stockage Media géré au lieu de simples URL HTTPS ;
- affectation parcours/catégories avec déblocages avancés ;
- back-office complet pour éditer tous les nouveaux objets sans JSON ;
- export effectif du journal et supervision de progression multi-utilisateur.
