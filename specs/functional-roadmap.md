# Roadmap fonctionnelle priorisée

Cette roadmap rapproche le backend distribué actuel de la cible produit d'origine. Le socle narratif livré reste la fondation ; la priorité passe désormais à la configuration exhaustive de la plateforme et du moteur, au RBAC, aux organisations école/entreprise/formation, puis à l'assistant/familier, à l'IA optionnelle et à l'économie.

## Règles transverses non négociables

Chaque nouvelle capacité doit, dans la même tranche verticale :

1. déclarer ses paramètres typés, leurs valeurs par défaut, leur portée et leur validation ;
2. déclarer les permissions nécessaires et les appliquer côté serveur, jamais seulement dans l'interface ;
3. couvrir au minimum un cas autorisé et un cas refusé par des tests ;
4. définir son comportement lorsque le module est désactivé ou sa configuration absente ;
5. auditer toute mutation sensible sans journaliser de secret ni de donnée personnelle ;
6. mettre à jour la matrice RBAC, les contrats, les specs, les tâches et les clients concernés ;
7. préserver un parcours jouable sans fournisseur cloud ni fournisseur d'IA.

Les références transverses sont [`platform-configuration.md`](platform-configuration.md) et le [`configuration-catalog.md`](configuration-catalog.md).

## Socle livré

- [x] état joueur riche, conditions/effets explicables et simulation bornée ;
- [x] interactions typées, gates, texte libre déterministe et confirmation ;
- [x] sauvegardes versionnées, migrations chaînées et replay golden ;
- [x] exploration de session, analyse de graphe et prévisualisation auteur ;
- [x] caractéristiques extensibles, effets différés, date logique et projections joueur ;
- [x] analyse d'entrée substituable et événements d'effets externes sans I/O.

## P0 — Configuration, RBAC et organisations

Objectif : rendre la plateforme administrable sans valeurs métier codées en dur et établir le garde-fou d'autorisation utilisé par tous les lots suivants.

- [x] registre documentaire typé, versionné et publiable avec défauts sûrs par front ;
- [x] dictionnaire extensible de libellés et vocabulaire du jeu, versionné, administrable et consommé par Web/iOS ;
- [ ] résolution hiérarchique documentée : plateforme → front/établissement → catégorie ou parcours → groupe/classe → utilisateur ;
- [x] séparation stricte entre paramètres publiables et références de secrets d'infrastructure ;
- [x] activation de modules par front sans contournement du RBAC ;
- [x] rôles personnalisables par composition de permissions stables, affectations portées et temporisées, policies serveur et endpoint des capacités effectives ;
- [ ] presets de départ adaptables : administrateur plateforme/organisation, auteur, publieur, enseignant/formateur/manager, participant, analyste, gestionnaire IA, magasin et modération ;
- [ ] audit des changements de configuration, rôles, permissions et affectations ;
- [ ] import/export d'une configuration portable, validée et versionnée ;
- [x] modèle `Front` typé `School`, `Company`, `TrainingProvider`, `Community` ou `Custom` ;
- [ ] profil d'organisation complet : identité, thème visuel, logo, typographies, calendrier et politiques par défaut ; la locale, le fuseau et la terminologie sont livrés ;
- [x] unités génériques hiérarchiques : établissements/classes/groupes ou entreprises/départements/équipes/cohortes ;
- [ ] memberships et liens encadrant–participant ;
- [x] catégories de scénarios configurables et ordonnées ;
- [ ] véritables parcours réutilisables au-dessus des catégories, avec relation N-N parcours↔catégories ;
- [ ] affectation de scénarios ou parcours à une classe/groupe avec disponibilité et échéance ;
- [ ] politiques pédagogiques configurables : tentatives, reprise, aide autorisée, seuil de réussite et visibilité des résultats ;
- [x] authentification locale/Entra/cumulative configurable, sans rendre OIDC obligatoire ;
- [ ] mapping de claims externes vers rôles ;
- [ ] règles d'accès combinant front, rôle, groupe, catégorie, publication et feature flags ;
- [ ] tests d'isolation garantissant qu'un acteur d'un établissement ne lit ni ne modifie celui d'un autre.

Premier incrément attendu : ADR des frontières, registre + résolution de configuration, catalogue de permissions initial, rôles custom et squelette organisation/unité/catégorie, avant toute fonction IA.

## P1 — Assistant/familier configurable et aide hors ligne

Objectif : fournir une aide contextuelle utile avant de dépendre d'un LLM.

- [ ] modèle neutre de familier : identité, apparence, références d'assets, licence, style d'écriture et capacités d'assistance ;
- [ ] configuration par défaut au niveau plateforme/front, limites par rôle et préférences utilisateur autorisées ;
- [ ] sélection et personnalisation persistées, avec fallback vers un familier système valide ;
- [ ] snapshot de configuration de l'assistant figé dans la session pour préserver le replay ;
- [ ] contexte minimal et non identifiant : parcours, catégorie, objectif, nœud, interaction, variables autorisées et historique récent borné ;
- [ ] aide statique/règles/indices auteurs pleinement fonctionnelle en mode hors ligne ;
- [ ] niveaux et fréquence d'intervention configurables ;
- [ ] import de familiers tiers derrière un adaptateur anti-corruption, dont Codex Pets, avec contrôle de licence ;
- [ ] permissions dédiées pour consulter, personnaliser, administrer et importer les familiers.

Le moteur `Narrative` porte uniquement les données déterministes nécessaires à la session et aux décisions d'aide. Les fournisseurs, prompts, credentials, quotas et appels réseau restent hors du moteur.

## P2 — IA optionnelle, provider-agnostic et maîtrisée

Objectif : augmenter l'assistant et l'authoring sans rendre le jeu, les tests ou Docker dépendants d'un service externe.

- [ ] ports métier `INarrativeAssistant`, `IInputAnalyzer` et `IQuestGenerator` sans type de fournisseur dans les contrats ;
- [ ] profils IA nommés et routage configurable par front, usage et catégorie ;
- [ ] fournisseurs Offline, local et cloud interchangeables, choix par configuration ;
- [ ] mode Offline déterministe activé par défaut et repli automatique sur aide statique ;
- [ ] sorties structurées, validées et bornées ; aucune sortie IA ne modifie directement l'état narratif ;
- [ ] confirmation humaine avant progression issue d'une interprétation libre et avant publication d'un contenu généré ;
- [ ] redaction/allowlist des données envoyées, protection contre l'injection, modération et timeouts ;
- [ ] métrage append-only des tokens/coûts avec pricing figé par appel et idempotence ;
- [ ] quotas plateforme/front/rôle/utilisateur avec résolution explicite et dégradation hors ligne ;
- [ ] permissions séparées pour utiliser l'IA, administrer les profils, lire les coûts, gérer pricing et quotas ;
- [ ] trace de décision reproductible : résultat accepté figé dans la commande ou la sauvegarde, jamais régénéré pendant un replay.

L'adoption éventuelle de `Microsoft.Extensions.AI` et de `IChatClient` dans l'infrastructure devra être confirmée par ADR au moment de l'implémentation. Aucun fournisseur particulier n'est une dépendance métier.

## P3 — Économie, magasin et récompenses configurables

Objectif : fournir une économie virtuelle entièrement pilotée par configuration et intégrée aux règles de déblocage, sans paiement réel implicite.

- [x] devise, solde initial et règles de gain par événement narratif configurables par front ;
- [x] wallet, ledger de gains/dépenses et idempotence des commandes ;
- [x] relais `Play -> PlayerExperience` des événements `economy.reward`, avec clés stables par session et séquence ;
- [ ] précision, plafonds et ajustements administratifs audités ;
- [ ] typologies de récompense extensibles : monnaie, titre, badge, cosmétique, familier, assistance, asset, collection, parcours ou scénario ;
- [x] premier catalogue d'offres, prix, activation et possessions configurables ;
- [ ] rayons, ordre et ciblage avancé ;
- [ ] stock, limites par joueur/groupe/période, conditions d'achat et règles de déblocage ;
- [ ] inventaire, entitlements, équipement et customisation du familier ;
- [ ] promotions virtuelles, bundles et événements saisonniers versionnés ;
- [x] historique wallet, garde-fous anti-double dépense et anti-double acquisition ;
- [ ] annulation et compensation métier ;
- [ ] permissions séparées pour consulter, acheter, gérer catalogue/prix/stock, ajuster un wallet et lire le ledger ;
- [ ] packs capables d'importer/exporter devises, récompenses, cosmétiques et boutiques.

Tout paiement en monnaie réelle nécessiterait un bounded context, un threat model et un ADR dédiés ; il n'est pas déduit de ce magasin virtuel.

## P4 — Expériences fonctionnelles augmentées

- [ ] dialogue contextuel du familier avec fallback déterministe ;
- [ ] analyse de réponse libre par rubrique, explication et confirmation ;
- [ ] copilote auteur : suggestions de nœuds, choix, indices et détection d'incohérences ;
- [x] génération de scénario en brouillon contextualisée par jeu/histoire/catégorie, conforme au schéma et validée par le moteur ;
- [ ] interactions document et photo avec workflow de validation ;
- [ ] diff et restauration fonctionnelle de versions ;
- [ ] reporting d'usage de l'aide et de l'IA par organisation, unité, catégorie et période, agrégé et pseudonymisé.

## P5 — Plateforme étendue configurable

- [ ] météo, présence et cycle jour/nuit comme contextes déterministes ;
- [ ] packs de préconfiguration portables ;
- [ ] compétences, hauts faits, certification et analytics avancés ;
- [ ] médias/documents, story arcs, aide/onboarding et espaces utilisateur ;
- [ ] notifications, temps réel, sondages, gouvernance/modération et LiveOps ;
- [ ] intégrations OIDC, LMS/LTI/xAPI, stockage, e-mail et webhooks derrière adaptateurs.

Ces lots ne doivent ni réimplémenter les règles narratives dans les clients, ni contourner le registre de configuration ou les policies RBAC.

La matrice exhaustive et honnête par domaine du plan initial est tenue dans [`product-capability-map.md`](product-capability-map.md). Une case livrée signifie qu’un parcours fonctionnel serveur et client est réellement utilisable, pas seulement qu’un modèle ou un écran existe.
