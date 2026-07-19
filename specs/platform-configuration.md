# Configuration de plateforme, organisations, RBAC et IA

Ce document fixe la cible fonctionnelle prioritaire issue du plan initial, adaptée à l'architecture distribuée actuelle. Il décrit les capacités attendues ; la création de nouveaux services ou le choix d'une bibliothèque nécessite un ADR séparé.

## Principes

- Une valeur métier configurable n'est pas codée en dur dans un client.
- Chaque paramètre possède un type, un schéma, une valeur par défaut sûre, une portée, une version et une règle de validation.
- Un secret n'entre jamais dans le registre lisible par les API d'administration ; il est référencé depuis un secret store d'infrastructure. La grammaire de ces références et leur résolution sont fixées ci-dessous, section [Références de secrets](#références-de-secrets).
- L'autorisation est évaluée côté service propriétaire de la ressource.
- Une configuration publiée ou utilisée par une session est identifiée par version ; aucune modification silencieuse ne change un replay.
- L'installation par défaut fonctionne avec un seul front générique, sans SSO, cloud ou IA.
- Le catalogue exhaustif des objets configurables est tenu dans [`configuration-catalog.md`](configuration-catalog.md).

## Résolution de configuration

La résolution suit l'ordre du plus spécifique au plus général :

`utilisateur → unité/groupe → catégorie/parcours → front/organisation → plateforme → défaut du schéma`

Toutes les clés n'acceptent pas toutes les portées. Leur déclaration indique les portées valides et la stratégie de fusion : remplacement, fusion d'objet ou union de liste. La réponse de résolution expose la valeur effective, sa version et sa provenance sans exposer les valeurs sensibles.

Une clé peut accepter une surcharge par rôle. Si un utilisateur cumule plusieurs rôles, la définition doit fournir une priorité explicite ; une égalité contradictoire rend la configuration invalide au lieu de choisir silencieusement.

Les feature flags ajoutent une décision d'activation par front puis, si nécessaire, par rôle ou cohorte. Ils ne remplacent jamais une permission : un flag active une capacité, une policy autorise un acteur à l'utiliser.

## Modèle d'organisation multi-contexte

Le vocabulaire métier reste générique afin de couvrir école, entreprise et formation :

| Concept | Fonction |
|---|---|
| `Front` | Organisation isolée typée école, entreprise, formation, communauté ou custom |
| `OperatingPeriod` | Année scolaire, semestre, exercice, campagne ou période de formation |
| `OrganizationUnit` | Classe, promotion, équipe, département ou cohorte, éventuellement hiérarchique |
| `Membership` | Appartenance d'un utilisateur à un groupe avec rôle et période |
| `Journey` | Parcours ordonné regroupant des catégories et objectifs |
| `ScenarioCategory` | Classification réutilisable : matière, compétence, univers ou thème |
| `Assignment` | Affectation d'un scénario/parcours à un groupe avec fenêtre et échéance |

Une catégorie peut appartenir à plusieurs parcours. Un scénario peut appartenir à plusieurs catégories. Les noms, descriptions, visuels, ordre, visibilité et règles de déblocage sont configurables par front. Les relations sont versionnées lorsqu'elles affectent une expérience publiée.

Le même modèle permet notamment : école → année scolaire → classe → apprenants, ou entreprise → exercice/campagne → département → équipe → collaborateurs. Les libellés et profondeurs sont configurables, avec un ou plusieurs enseignants, formateurs ou managers responsables.

### Paramètres d'une organisation

- identité publique, nom court, logo, design tokens, domaine et mentions légales ;
- locale, langues disponibles, fuseau horaire, formats de date et terminologie affichée (`classe`, `promotion`, `équipe`...) ;
- calendrier, jours ouvrés, périodes actives et règles d'archivage ;
- modes d'authentification activés et mapping de claims externes, les secrets restant dans l'infrastructure ;
- rôles par défaut, politique d'invitation/inscription et capacités accessibles ;
- modules actifs, catégories visibles, parcours proposés et familier par défaut ;
- politique d'aide/IA, conservation, quotas et visibilité des analytics.

### Marque et démarrage client anonyme

L'identité publique, le nom court, le logo et les design tokens listés ci-dessus sont portés par le bloc **`branding`** du document d'expérience. Il est facultatif et purement additif : une configuration antérieure sans ce bloc reste publiable et lisible sans changement, et un client retombe alors sur ses propres défauts (« GenEngine » pour le nom d'application). Sa `accentPalette` associe les jetons d'accent nommés déjà portés par les catégories, les parcours et les familiers à de vraies couleurs, ce qui les rend enfin rendables. La grammaire complète, les jetons de thème obligatoires et les règles de validation (`invalid_branding`) sont dans [`api/http.md`](api/http.md).

Un client démarre avant toute authentification et a besoin de savoir quoi afficher. `GET /client-bootstrap/{frontId}`, anonyme, lui livre le strict nécessaire : identité et marque, locale, fuseau, libellés, scènes d'introduction, mode d'authentification et disponibilité de la démonstration. Elle ne porte **aucun** catalogue, aucune organisation, aucune affectation, aucun provider IA et aucun identifiant de locataire Entra ; ces derniers restent publiés par Identity sur `GET /auth/providers`, qui en est la source unique. La répartition champ par champ entre les trois surfaces de lecture est fixée par le tableau de [`api/http.md`](api/http.md).

### Paramètres d'une unité ou d'un groupe

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

Un parcours représente une durée très variable — un semestre, une année, une formation entière — et peut porter jusqu'à environ 200 scénarios. Plusieurs parcours peuvent partager une même catégorie : c'est un besoin produit explicite, jamais une erreur de saisie.

Le graphe de `prerequisiteJourneyIds` doit rester **acyclique**, au même titre que la hiérarchie des unités d'organisation. Un cycle, même transitif (`A → B → A`, `A → B → C → A`), verrouille définitivement tous les parcours de la boucle : aucun joueur ne peut plus en satisfaire les prérequis. `PUT /admin/configuration/{frontId}` refuse un tel document avec `journey_cycle` ; l'auto-référence reste refusée avec `invalid_journey`.

Un parcours est **déverrouillé** lorsque tous ses prérequis directs sont terminés, un parcours étant terminé lorsque chacun de ses scénarios a atteint au moins une fin, sur n'importe laquelle de ses versions publiées.

Un parcours **à périmètre vide compte comme trivialement terminé** et ne verrouille donc rien. Le périmètre devient vide de plusieurs façons qu'aucune validation ne peut prévenir durablement : un jalon publié sans contenu, des catégories vidées, ou des `categoryIds` pointant vers des catégories supprimées après publication. Exiger qu'un tel parcours soit terminé verrouillerait définitivement tous ses successeurs, pour tous les joueurs, sans qu'aucune action ne puisse rétablir la situation — précisément l'impasse que la validation du graphe existe pour empêcher. Échouer en ouvrant est réparable, un opérateur ajoute les scénarios et la porte se met à fonctionner ; échouer en fermant ne l'est pas. Le pourcentage d'un parcours vide reste néanmoins à zéro : l'achèvement mesure ce qui reste dû, le pourcentage mesure le travail accompli, et afficher 100 % sur un parcours vide mentirait au joueur.

Chaque joueur choisit **un parcours par défaut**, stocké dans son profil `PlayerExperience` et non dans la configuration. Il est facultatif : une configuration et un profil antérieurs continuent de fonctionner sans lui.

## RBAC et rôles personnalisables

Les permissions sont des contrats stables enregistrés par les services. Un administrateur autorisé compose des rôles custom à partir de ces permissions, les clone, les versionne, les active ou les archive, sans créer de permission arbitraire inconnue du backend.

Une affectation de rôle indique une portée (`platform`, `front`, `unit`, `group` ou ressource compatible), une date de début et une date de fin optionnelles. La résolution produit les permissions effectives et leur provenance. Les presets ne sont que des modèles initiaux ; aucun nom de rôle n'est codé dans une règle métier.

| Domaine | Permissions initiales |
|---|---|
| Configuration | `config.read`, `config.write`, `config.publish`, `module.toggle` |
| Identité/RBAC | `identity.user.read`, `identity.user.manage`, `rbac.read`, `rbac.manage` |
| Organisation | `front.read`, `front.manage`, `unit.read`, `unit.manage`, `period.read`, `period.manage`, `membership.read`, `membership.manage` |
| Catalogue | `journey.read`, `journey.manage`, `category.read`, `category.manage`, `assignment.manage` |
| Scénarios | `scenario.read`, `scenario.author`, `scenario.review`, `scenario.publish` |
| Jeu | `session.play`, `session.read.own`, `session.read.group`, `session.manage` |
| Assistant | `assistant.use`, `assistant.customize`, `assistant.manage`, `assistant.import` |
| IA | `ai.use`, `ai.profile.manage`, `ai.usage.read`, `ai.pricing.manage`, `ai.quota.manage` |
| Économie | `shop.read`, `shop.buy`, `shop.manage`, `economy.reward.manage`, `wallet.read.own`, `wallet.read.scope`, `wallet.adjust`, `economy.ledger.read` |
| Insights | `insights.read.own`, `insights.read.scope`, `insights.manage`, `insights.export` |
| Gouvernance | `moderation.review`, `consent.manage`, `privacy.request.manage` |
| Audit | `audit.read` |

Les endpoints listant les capacités effectives permettent aux clients d'adapter leur interface, mais l'absence d'un contrôle visuel ne vaut jamais autorisation. Toute permission ajoutée doit rejoindre cette matrice, les presets concernés et des tests allow/deny dans la même PR. Les mutations RBAC empêchent l'auto-élévation, la suppression du dernier administrateur plateforme et les affectations hors de la portée de l'opérateur ; elles sont toutes auditées.

## Ownership distribué cible

| Bounded context | Responsabilité | Exclusions explicites |
|---|---|---|
| `Identity` | Comptes, rôles, permissions, affectations de rôles et émission des capacités signées | Classes, catégories, configuration produit et préférences de familier |
| `Configuration` | Fronts, registre typé, résolution, versions, feature flags, modules et branding | Secrets, comptes et règles narratives |
| `Organization` | Types de front métier, périodes, unités/classes/équipes, memberships, encadrement et affectations | Credentials et contenu des scénarios |
| `Authoring` | Scénarios, parcours, catégories, relations éditoriales et publication | Comptes, classes et exécution de sessions |
| `Play` | Sessions, préférences joueur utiles au jeu, snapshots effectifs et consommation d'aide | Administration des providers et catalogue maître de familiers |
| `Assistant` | Catalogue neutre de familiers, politiques d'aide, profils/providers IA, exécution des adaptateurs, metering et quotas | Mutation directe de l'état narratif |
| `Economy` | Devises, wallets, ledger, récompenses, inventaires, magasins, offres et achats | Paiement réel et mutation directe d'une session |
| `Narrative` | Snapshots déterministes validés et décisions métier pures | Tenant, compte, secret, provider, quota, réseau et persistance |

`Configuration`, `Organization`, `Assistant` et `Economy` sont des services autonomes candidats, chacun avec sa base et ses contrats versionnés. Un ADR doit confirmer leurs frontières et l'ordre d'introduction avant création des projets. Ils ne seront ni des modules internes d'un déployable global, ni des tables ajoutées aux bases existantes par commodité.

Le service propriétaire d'une ressource applique les policies localement à partir de capacités signées et de la portée de la ressource. Les relations interservices utilisent des identifiants stables et des contrats explicites, jamais des foreign keys entre bases ni des références de projet.

## Assistant et familier

Le modèle neutre comprend : identifiant, nom, apparence et assets, licence, traits de style, niveau/fréquence d'aide, capacités autorisées et fallback. Les préférences utilisateur sont bornées par la configuration du front et le RBAC.

Au démarrage d'une session, `Play` fige la configuration effective utile au runtime dans un snapshot versionné. Une évolution administrative s'applique aux nouvelles sessions ou via une migration explicite, jamais silencieusement.

Le contexte transmis à une aide externe est construit sur allowlist et exclut identifiants, email, texte privé non requis et secrets. L'assistant propose ; une action ayant un effet narratif repasse par une commande métier validée.

## IA, coûts et repli

Les adaptateurs IA restent en Infrastructure derrière des ports métier. Les profils nommés décrivent fournisseur, modèle, usage, limites et politique de fallback sans faire fuiter ces détails dans `Narrative`.

Le chemin nominal de développement et CI est `Offline` : aide statique et réponses déterministes. Les modes local et cloud sont opt-in. Une panne, un quota atteint ou une sortie invalide déclenche une dégradation explicite, jamais un blocage du jeu.

Chaque appel accepté possède un `callId` idempotent et un enregistrement append-only : front, acteur pseudonymisé, session, usage, profil/modèle, tokens entrée/sortie, tarif appliqué, coût, horodatage et résultat de fallback. Les tarifs sont versionnés et le coût historique ne change pas lors d'une mise à jour.

## Références de secrets

Un fournisseur externe — aujourd'hui `AiProviderDefinition` — se configure avec une
`secretReference`. Cette référence **désigne** un secret, elle ne le **contient** pas. Le
document de configuration, la base `Configuration`, les journaux, les traces et les messages
d'erreur ne portent jamais la valeur du secret.

### Grammaire

```abnf
reference  = scheme ":" identifier
scheme     = [a-z] [a-z0-9-]*          ; minuscules ASCII, obligatoire
identifier = 1*( VCHAR )               ; non vide, sans espace ni caractère de contrôle
```

La longueur totale est bornée à 512 caractères. Une référence vide ou absente signifie
« ce fournisseur ne demande aucun identifiant » ; toute autre valeur doit être conforme,
faute de quoi l'enregistrement est refusé en `invalid_secret_reference`.

| Schéma | État | Identifiant | Résolution |
| --- | --- | --- | --- |
| `env` | **implémenté** | Nom de variable `[A-Z_][A-Z0-9_]*` | Environnement du processus, injecté par Compose ou la plateforme d'hébergement. |
| `vault` | **réservé, non implémenté** | Chemin dans le coffre-fort | Aucun résolveur n'est enregistré : la référence dégrade en `secret_scheme_unsupported`. |

Le choix d'`env` comme unique implémentation locale est délibéré : il ne lit ni n'écrit rien
sur le disque, ce qui reste compatible avec le conteneur en lecture seule et l'utilisateur
non-root. Aucun schéma « fichier » n'est prévu, pour ne pas encourager des secrets déposés
sur le système de fichiers.

### Extensibilité

Ajouter un backend, c'est enregistrer un `ISecretResolver` supplémentaire dans le
`SecretStore` ; la grammaire ne change pas. Un coffre-fort — Azure Key Vault ou équivalent —
prendra la place réservée par le schéma `vault`. Ce dépôt ne livre **aucun client de
coffre-fort** : il ne peut pas être testé ici honnêtement.

### Dégradation

L'échec de résolution est une **valeur**, jamais une exception. Un fournisseur dont le secret
est introuvable est vu comme non configuré et l'appelant se replie sur le fournisseur
`Offline`, conformément à la section précédente. La cause remontée est un code stable et
clos — `secret_not_configured`, `secret_reference_malformed`, `secret_scheme_unsupported`,
`secret_not_found` — qui ne transporte ni la référence, ni le chemin du backend, ni un
fragment du secret. Une exception levée par un backend est rabattue sur `secret_not_found`
pour la même raison.

Le secret résolu est porté par un `SecretValue` dont tous les rendus implicites —
`ToString`, interpolation, formatage de log structuré, sérialisation JSON — produisent `***`.
Seul un appel délibéré à `Reveal()` rend la valeur claire ; c'est le point unique à auditer,
et il n'a lieu qu'au moment de tendre l'identifiant au fournisseur externe.

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
