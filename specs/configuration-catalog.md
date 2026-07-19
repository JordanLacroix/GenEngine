# Catalogue fonctionnel de configuration

Ce catalogue constitue la checklist cible de la plateforme GenEngine. Il évite que « tout configurable » devienne une collection de JSON sans contrat : chaque objet possède un schéma typé, des défauts sûrs, des portées autorisées, une validation, une version, un audit et des permissions explicites.

## Cycle de vie commun

Selon sa nature, une configuration suit tout ou partie du cycle : brouillon → validation → prévisualisation/diff → approbation → publication planifiée → active → archivée. Une version publiée est immutable ; restauration et rollback créent une nouvelle version. Import/export passe par un format ouvert avec schéma, compatibilité, dépendances, checksums et rapport de validation.

Les portées possibles sont `platform`, `front`, `unit`, `group`, `journey`, `category`, `scenario`, `role` et `user`. Chaque définition limite explicitement les portées qu'elle accepte. Les secrets sont remplacés par des références opaques résolues uniquement dans l'infrastructure.

## Matrice des domaines configurables

| Domaine | Objets et paramètres configurables | Permissions minimales |
|---|---|---|
| Plateforme | Fronts, type d'organisation, modules, feature flags, branding, design tokens, langues, fuseau, terminologie, domaines et rétention | `config.*`, `front.*`, `module.toggle` |
| Identité | Auth locale, providers OIDC/OAuth2, politiques de compte, claim mappings, provisioning JIT, sessions, rôles custom et affectations portées | `identity.*`, `auth.provider.*`, `rbac.*` |
| Organisation | Périodes, unités hiérarchiques, classes, promotions, départements, équipes, cohortes, responsables, memberships, import de masse et affectations | `period.*`, `unit.*`, `membership.*`, `membership.import.*`, `assignment.*` |
| Catalogue | Parcours, catégories N-N, tags, ordre, visibilité, planning, prérequis, règles de déblocage et scénarios optionnels | `journey.*`, `category.*`, `catalog.*` |
| Moteur narratif | Types d'interaction activés, caractéristiques, registres de conditions/effets, budgets de complexité, politique de sauvegarde/replay et contextes temps/météo/présence | `engine.config.*`, `scenario.author` |
| Authoring | Templates, schémas, lint rules, seuils, workflow auteur/revue/publication, preview, diff/restore, imports/exports et limites média | `scenario.*`, `authoring.config.*` |

Le catalogue de parcours reste porté par le document de configuration et édité par `PUT /admin/configuration/{frontId}`. `journey.manage` ouvre une vue d'exploitation en lecture seule (`GET /admin/journeys/{frontId}`) et non un second chemin d'écriture, qui ferait courir le Studio et l'Administration sur la même révision optimiste. La validation refuse un graphe de prérequis cyclique avec `journey_cycle` et une auto-référence avec `invalid_journey`. Le partage d'une catégorie entre plusieurs parcours est explicitement autorisé. Le parcours par défaut d'un joueur n'appartient pas à ce registre : il vit dans son profil `PlayerExperience`, sous `journey.read`.

L'import de memberships est une politique d'exploitation du service propriétaire : activé par défaut, limité à 500 lignes par commande et borné entre 1 et 5 000. `Organization:MembershipImport:Enabled=false` le désactive explicitement avec `membership_import_disabled`; `Organization:MembershipImport:MaxRows` règle la limite plateforme. L'API effectue toujours une prévalidation complète avant écriture et n'applique aucune ligne si le rapport contient une erreur.
| Jeu | Politique de session, pause/reprise, tentatives, reprise après échec, visibilité de l'arbre, aides, résultats et idempotence | `session.*`, `play.config.*` |
| Assistant | Familiers, assets/licences, style, ton, fréquence, capacités, niveaux d'aide, indices offline et préférences autorisées | `assistant.*` |
| IA | Providers, modèles, profils, routage, fallback, double avis, prompts, structured outputs, tools autorisés, cache, résilience, sûreté, pricing et quotas | `ai.*` |
| Économie | Devises, wallets, reward types, inventaires, shops, offres, prix, stock, limites, promotions, entitlements et compensations | `shop.*`, `wallet.*`, `economy.*` |
| Médias | Types/taille, stockage, rétention, scan, licences, consentement, validation document/photo et règles d'accès | `media.*` |
| StoryArc | Arcs, chapitres, cadence, continuité, variations, fallback statique et règles de déblocage | `storyarc.*` |
| Packs | Manifeste, dépendances, compatibilité, licences, preview, stratégie de conflit, application/rollback et catalogue | `pack.*` |
| Insights | KPI, formules déclaratives, dimensions, cohortes, seuils, objectifs, dashboards, anonymat, exports et rétention | `insights.*` |
| Profil/Pédagogie | Caractéristiques, titres, badges, journal, collection, référentiels, compétences, hauts faits et certifications | `profile.*`, `competency.*` |
| Builder | Catalogue de widgets, layouts par défaut front/rôle, limites de personnalisation et reset | `builder.*` |
| Aide/Onboarding | Articles, FAQ, glossaire, recherche, tutoriels, étapes, ciblage et progression | `help.*`, `onboarding.*` |
| Notifications | Templates i18n, événements, canaux, préférences, horaires, relances, digest et limites | `notification.*` |
| Realtime | Présence agrégée, granularité, transport, fallback polling et limites de diffusion | `realtime.*` |
| Gouvernance | Consentements, confidentialité, export/effacement, modération, signalements, règles de contenu et audit | `governance.*`, `moderation.*` |
| Survey | Formulaires, questions, branchements simples, anonymat, déclencheurs et agrégation | `survey.*` |
| LiveOps | Saisons, événements, ciblage, fenêtres, thèmes, variations, boutique éphémère et rollback | `liveops.*` |
| Intégrations | LMS/LTI/xAPI, webhooks, e-mail, stockage, analytics externes et mappings par adaptateurs | `integration.*` |

`*` désigne ici une famille lisible ; les policies utilisent toujours des codes de permission explicites, jamais un wildcard accordé dans un jeton.

## Organisations : école, entreprise et autres fronts

Un `Front` déclare un `OrganizationType` parmi `School`, `Company`, `TrainingProvider`, `Community` et `Custom`. Ce type choisit uniquement des presets de terminologie, modules et rôles ; il ne change pas les invariants ou les contrats.

| Contexte | Périodes usuelles | Unités usuelles | Encadrants | Participants |
|---|---|---|---|---|
| École | année, semestre | établissement, promotion, classe | enseignant, responsable pédagogique | étudiant, apprenant |
| Entreprise | exercice, campagne | filiale, département, équipe | manager, RH, formateur | collaborateur |
| Formation | session, cohorte | organisme, programme, groupe | formateur, tuteur | stagiaire |
| Communauté/custom | saison, période libre | espace, cercle, équipe | animateur | membre |

La terminologie visible est configurable. Les policies ne testent jamais les libellés de rôles (`Teacher`, `Manager`) : elles testent des permissions et une portée de ressource.

## RBAC custom

### Modèle

- `PermissionDefinition` : code stable, service propriétaire, description, niveau de risque et scopes compatibles ;
- `RoleTemplate` : preset livré par la plateforme ou un pack, jamais requis par le code métier ;
- `CustomRole` : nom, description, ensemble de permissions, scope maximal, version, statut et front propriétaire ;
- `RoleAssignment` : utilisateur ou groupe, rôle, scope effectif, validité temporelle et auteur de l'affectation ;
- `EffectiveCapability` : résultat calculé avec permission, scope, provenance et expiration.

Les administrateurs peuvent créer, cloner, modifier, versionner et archiver des rôles custom. Ils ne peuvent accorder qu'une permission qu'ils possèdent eux-mêmes avec un scope au moins équivalent. Le système protège le dernier administrateur plateforme, empêche l'auto-élévation et demande une confirmation renforcée pour les permissions critiques.

Les rôles sont allow-only au départ pour garder une résolution explicable. Un éventuel mécanisme de deny ou d'héritage complexe nécessite un ADR et des tests de conflit. Les changements sont audités et les sessions/tokens sont réévalués selon une politique de révocation documentée.

### Presets initiaux

- administrateur plateforme ;
- administrateur d'organisation/front ;
- gestionnaire d'identité et RBAC ;
- auteur, reviewer et publisher ;
- enseignant, formateur ou manager ;
- participant/apprenant ;
- analyste/lecteur de reporting ;
- gestionnaire assistant/IA et lecteur de coûts ;
- gestionnaire économie/magasin ;
- support et modérateur.

Chaque front peut remplacer ces presets par ses rôles custom sans modifier le code.

## Configuration du moteur narratif

Le registre expose seulement des choix bornés par le moteur, jamais du code arbitraire :

- versions de schéma acceptées et politique de migration ;
- interactions activées et limites par scénario/catégorie/front ;
- définitions de caractéristiques, bornes, valeurs initiales et visibilité ;
- conditions/effets disponibles, paramètres autorisés et budgets d'exécution ;
- budgets de graphe : nœuds, profondeur, branches, boucles, texte et effets différés ;
- règles de validation : liens, atteignabilité, fins, ambiguïtés, assets et accessibilité ;
- machine de session : tentatives, pause, reprise, expiration, abandon et validation externe ;
- sauvegarde : cadence, rétention, compatibilité, export et restauration ;
- contexte logique : calendrier narratif, météo, lumière, présence simulée et graine quotidienne ;
- règles de révélation de l'arbre, journal, collection, synthèse et rejouabilité.

Toute valeur affectant un replay est compilée dans le snapshot publié ou figée dans la session avec sa version et son hash.

## Providers et profils IA

### Provider

Une définition de provider contient : type d'adaptateur, endpoint, référence de secret, région/résidence, capacités (`chat`, `embedding`, `vision`, `image` si supportées), modèles disponibles, health check et statut. Les providers initiaux visés sont `Offline`, un endpoint local tel qu'Ollama, OpenAI-compatible et Azure AI Foundry ; aucune dépendance métier ne cite l'un d'eux.

### Profil

Un profil nommé configure : provider/modèle, usage, température, graine si supportée, tokens maximum, contexte, structured output, outils autorisés, prompt template versionné, règles de redaction, modération, cache, timeout, retry, circuit breaker, concurrence, tarif et chaîne de fallback.

Le routage sélectionne un profil par front, catégorie, rôle, classification de données et usage (`assistant`, `input-analysis`, `authoring`, `quest-generation`, `moderation`, `embedding`). Une règle peut demander deux profils pour un double avis ; leurs résultats restent séparés et la décision finale est explicite.

Les prompts sont du contenu versionné avec variables allowlistées, tests golden et possibilité de rollback. Les credentials, clés et tokens ne figurent jamais dans le prompt ou la configuration exportable.

### Maîtrise opérationnelle

- quotas tokens/coûts/appels/concurrence par plateforme, front, rôle, groupe et utilisateur ;
- pricing entrée/sortie versionné par modèle, devise et date d'effet ;
- ledger d'usage idempotent et tableaux de bord par usage/période/scope ;
- seuils et alertes, arrêt, fallback offline ou demande d'approbation au dépassement ;
- redaction PII, allowlist de contexte, garde anti-injection, validation de sortie et modération ;
- mode Offline par défaut en développement, tests et Docker sans clé ;
- résultat accepté figé avant toute utilisation déterministe ou publication.

## Économie et magasin

### Modèle configurable

- `Currency` : code, libellé, icône, précision, plafond, règles d'expiration et portées ;
- `WalletPolicy` : soldes autorisés, gains/dépenses, limites et visibilité ;
- `RewardType` : monnaie, titre, badge, cosmétique, familier, capacité d'aide, asset, collection, entitlement, parcours ou scénario ;
- `Shop` et `ShopSection` : identité, audience, ordre, filtres, fenêtre et thème ;
- `Offer` : item ou bundle, prix, devise, stock, limite, période, cible et `UnlockRule` ;
- `InventoryItem`/`Entitlement` : ownership, quantité, état équipé et expiration ;
- `Purchase` et `CurrencyTransaction` : ledgers append-only, idempotents et auditables.

Les gains proviennent d'effets métier validés, de complétions, d'achievements ou d'ajustements administratifs autorisés. Les achats vérifient atomiquement solde, stock, limites, disponibilité et conditions. Un retry ne débite ni ne crédite deux fois.

Les admins configurent catalogues, prix virtuels, récompenses, ciblage et stock ; un rôle distinct autorise les ajustements de wallet. Les joueurs consultent le magasin et leur historique selon les permissions. Les analytics agrégés n'exposent pas le ledger personnel sans scope approprié.

Promotions, bundles, boutique saisonnière et cosmétiques de familier utilisent les mêmes objets versionnés. Le réel paiement, la fiscalité et le remboursement bancaire restent hors scope tant qu'un bounded context Payment n'est pas explicitement décidé.

## Packs de configuration

Un `ContentPack` peut combiner configuration de front, terminologie, rôles templates, parcours/catégories/scénarios, familiers, prompts sans secrets, providers logiques, devises, récompenses, boutiques, KPI, aide et assets licenciés.

L'application d'un pack produit un plan de changement, détecte les conflits, valide les dépendances et licences, permet une prévisualisation, exige les permissions de chaque domaine touché et conserve une trace de provenance. Aucun pack ne peut accorder à son opérateur des permissions qu'il ne possède pas.

## Règle d'entretien

Toute nouvelle fonctionnalité met à jour dans la même PR :

1. sa ligne ou section de ce catalogue ;
2. ses clés typées, défauts, portées et stratégie de résolution ;
3. ses permissions, scopes et presets impactés ;
4. ses tests allow/deny, isolation et comportement désactivé ;
5. son audit, son fallback et ses données sensibles ;
6. ses tâches, contrats et clients concernés.
