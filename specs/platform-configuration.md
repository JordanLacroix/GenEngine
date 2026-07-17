# Configuration de plateforme, contexte scolaire, RBAC et IA

Ce document fixe la cible fonctionnelle prioritaire issue du plan initial, adaptée à l'architecture distribuée actuelle. Il décrit les capacités attendues ; la création de nouveaux services ou le choix d'une bibliothèque nécessite un ADR séparé.

## Principes

- Une valeur métier configurable n'est pas codée en dur dans un client.
- Chaque paramètre possède un type, un schéma, une valeur par défaut sûre, une portée, une version et une règle de validation.
- Un secret n'entre jamais dans le registre lisible par les API d'administration ; il est référencé depuis un secret store d'infrastructure.
- L'autorisation est évaluée côté service propriétaire de la ressource.
- Une configuration publiée ou utilisée par une session est identifiée par version ; aucune modification silencieuse ne change un replay.
- L'installation par défaut fonctionne avec un seul front, sans école, SSO, cloud ou IA.

## Résolution de configuration

La résolution suit l'ordre du plus spécifique au plus général :

`utilisateur → groupe/classe → catégorie/parcours → front/établissement → plateforme → défaut du schéma`

Toutes les clés n'acceptent pas toutes les portées. Leur déclaration indique les portées valides et la stratégie de fusion : remplacement, fusion d'objet ou union de liste. La réponse de résolution expose la valeur effective, sa version et sa provenance sans exposer les valeurs sensibles.

Les feature flags ajoutent une décision d'activation par front puis, si nécessaire, par rôle ou cohorte. Ils ne remplacent jamais une permission : un flag active une capacité, une policy autorise un acteur à l'utiliser.

## Modèle établissement et école

Le vocabulaire métier reste générique afin de couvrir école, entreprise et formation :

| Concept | Fonction |
|---|---|
| `Front` | Organisation isolée, branding, locale, modules et politiques par défaut |
| `AcademicPeriod` | Année, semestre, promotion ou période de formation |
| `Group` | Classe, promotion, équipe ou département, éventuellement hiérarchique |
| `Membership` | Appartenance d'un utilisateur à un groupe avec rôle et période |
| `Journey` | Parcours ordonné regroupant des catégories et objectifs |
| `ScenarioCategory` | Classification réutilisable : matière, compétence, univers ou thème |
| `Assignment` | Affectation d'un scénario/parcours à un groupe avec fenêtre et échéance |

Une catégorie peut appartenir à plusieurs parcours. Un scénario peut appartenir à plusieurs catégories. Les noms, descriptions, visuels, ordre, visibilité et règles de déblocage sont configurables par front. Les relations sont versionnées lorsqu'elles affectent une expérience publiée.

Le modèle doit permettre au minimum : établissement → année scolaire → classe → apprenants, avec un ou plusieurs enseignants responsables, sans imposer cette hiérarchie aux fronts non scolaires.

### Paramètres d'un établissement

- identité publique, nom court, logo, design tokens, domaine et mentions légales ;
- locale, langues disponibles, fuseau horaire, formats de date et terminologie affichée (`classe`, `promotion`, `équipe`...) ;
- calendrier, jours ouvrés, périodes actives et règles d'archivage ;
- modes d'authentification activés et mapping de claims externes, les secrets restant dans l'infrastructure ;
- rôles par défaut, politique d'invitation/inscription et capacités accessibles ;
- modules actifs, catégories visibles, parcours proposés et familier par défaut ;
- politique d'aide/IA, conservation, quotas et visibilité des analytics.

### Paramètres d'une classe ou d'un groupe

- libellé, code, période, parent éventuel, responsables et membres ;
- statut brouillon/actif/archivé, dates d'ouverture et de fermeture ;
- parcours/scénarios affectés, disponibilité, échéance et ordre conseillé ;
- nombre de tentatives, reprise autorisée, seuil de réussite, aides disponibles et visibilité des résultats ;
- surcharges de configuration limitées aux clés explicitement autorisées à la portée groupe.

### Paramètres des parcours et catégories

- identifiant stable, libellés localisés, description, visuel, ordre et statut ;
- relations N-N parcours–catégories et catégories–scénarios ;
- prérequis, règles de déblocage, publics/rôles autorisés et fenêtre de disponibilité ;
- objectifs pédagogiques, tags de recherche et politique d'aide par défaut ;
- version des relations publiée avec le catalogue afin qu'une session ne change pas silencieusement.

## Matrice RBAC initiale

Les rôles sont des presets modifiables ; les permissions sont les contrats stables utilisés par les policies.

| Domaine | Permissions initiales |
|---|---|
| Configuration | `config.read`, `config.write`, `config.publish`, `module.toggle` |
| Identité/RBAC | `identity.user.read`, `identity.user.manage`, `rbac.read`, `rbac.manage` |
| Établissement | `front.read`, `front.manage`, `group.read`, `group.manage`, `membership.manage` |
| Catalogue | `journey.read`, `journey.manage`, `category.read`, `category.manage`, `assignment.manage` |
| Scénarios | `scenario.read`, `scenario.author`, `scenario.review`, `scenario.publish` |
| Jeu | `session.play`, `session.read.own`, `session.read.group`, `session.manage` |
| Assistant | `assistant.use`, `assistant.customize`, `assistant.manage`, `assistant.import` |
| IA | `ai.use`, `ai.profile.manage`, `ai.usage.read`, `ai.pricing.manage`, `ai.quota.manage` |
| Audit | `audit.read` |

Les endpoints listant les capacités effectives permettent aux clients d'adapter leur interface, mais l'absence d'un contrôle visuel ne vaut jamais autorisation. Toute permission ajoutée doit rejoindre cette matrice, les presets concernés et des tests allow/deny dans la même PR.

## Ownership distribué cible

| Bounded context | Responsabilité | Exclusions explicites |
|---|---|---|
| `Identity` | Comptes, rôles, permissions, affectations de rôles et émission des capacités signées | Classes, catégories, configuration produit et préférences de familier |
| `Configuration` | Fronts, registre typé, résolution, versions, feature flags, modules et branding | Secrets, comptes et règles narratives |
| `Organization` | Périodes, groupes/classes, memberships, encadrement et affectations | Credentials et contenu des scénarios |
| `Authoring` | Scénarios, parcours, catégories, relations éditoriales et publication | Comptes, classes et exécution de sessions |
| `Play` | Sessions, préférences joueur utiles au jeu, snapshots effectifs et consommation d'aide | Administration des providers et catalogue maître de familiers |
| `Assistant` | Catalogue neutre de familiers, politiques d'aide, profils IA, exécution des adaptateurs, metering et quotas | Mutation directe de l'état narratif |
| `Narrative` | Snapshots déterministes validés et décisions métier pures | Tenant, compte, secret, provider, quota, réseau et persistance |

`Configuration`, `Organization` et `Assistant` sont des services autonomes candidats, chacun avec sa base et ses contrats versionnés. Un ADR doit confirmer leurs frontières et l'ordre d'introduction avant création des projets. Ils ne seront ni des modules internes d'un déployable global, ni des tables ajoutées aux bases existantes par commodité.

Le service propriétaire d'une ressource applique les policies localement à partir de capacités signées et de la portée de la ressource. Les relations interservices utilisent des identifiants stables et des contrats explicites, jamais des foreign keys entre bases ni des références de projet.

## Assistant et familier

Le modèle neutre comprend : identifiant, nom, apparence et assets, licence, traits de style, niveau/fréquence d'aide, capacités autorisées et fallback. Les préférences utilisateur sont bornées par la configuration du front et le RBAC.

Au démarrage d'une session, `Play` fige la configuration effective utile au runtime dans un snapshot versionné. Une évolution administrative s'applique aux nouvelles sessions ou via une migration explicite, jamais silencieusement.

Le contexte transmis à une aide externe est construit sur allowlist et exclut identifiants, email, texte privé non requis et secrets. L'assistant propose ; une action ayant un effet narratif repasse par une commande métier validée.

## IA, coûts et repli

Les adaptateurs IA restent en Infrastructure derrière des ports métier. Les profils nommés décrivent fournisseur, modèle, usage, limites et politique de fallback sans faire fuiter ces détails dans `Narrative`.

Le chemin nominal de développement et CI est `Offline` : aide statique et réponses déterministes. Les modes local et cloud sont opt-in. Une panne, un quota atteint ou une sortie invalide déclenche une dégradation explicite, jamais un blocage du jeu.

Chaque appel accepté possède un `callId` idempotent et un enregistrement append-only : front, acteur pseudonymisé, session, usage, profil/modèle, tokens entrée/sortie, tarif appliqué, coût, horodatage et résultat de fallback. Les tarifs sont versionnés et le coût historique ne change pas lors d'une mise à jour.

## Definition of Done d'une fonctionnalité

Une fonctionnalité n'est terminée que si la PR répond explicitement à ces questions :

- Quels paramètres, défauts, portées et migrations ajoute-t-elle ?
- Quel flag ou module la contrôle, et quel est son comportement désactivé ?
- Quelles permissions protègent lecture, usage et administration ?
- Quels rôles initiaux les reçoivent ?
- Quels tests prouvent un accès autorisé, un refus et l'isolation de front si applicable ?
- Quelles actions sont auditées ?
- Quel est le comportement hors ligne, sans IA et quota dépassé ?
- Quels contrats et clients doivent afficher la capacité effective ?
