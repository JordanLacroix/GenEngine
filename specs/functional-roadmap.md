# Roadmap fonctionnelle priorisée

Cette roadmap rapproche le backend distribué actuel de la cible produit d'origine. Le socle narratif livré reste la fondation ; la priorité passe désormais à la configuration de la plateforme, au RBAC, au contexte scolaire, puis à l'assistant/familier et à l'IA optionnelle.

## Règles transverses non négociables

Chaque nouvelle capacité doit, dans la même tranche verticale :

1. déclarer ses paramètres typés, leurs valeurs par défaut, leur portée et leur validation ;
2. déclarer les permissions nécessaires et les appliquer côté serveur, jamais seulement dans l'interface ;
3. couvrir au minimum un cas autorisé et un cas refusé par des tests ;
4. définir son comportement lorsque le module est désactivé ou sa configuration absente ;
5. auditer toute mutation sensible sans journaliser de secret ni de donnée personnelle ;
6. mettre à jour la matrice RBAC, les contrats, les specs, les tâches et les clients concernés ;
7. préserver un parcours jouable sans fournisseur cloud ni fournisseur d'IA.

La référence transverse est [`platform-configuration.md`](platform-configuration.md).

## Socle livré

- [x] état joueur riche, conditions/effets explicables et simulation bornée ;
- [x] interactions typées, gates, texte libre déterministe et confirmation ;
- [x] sauvegardes versionnées, migrations chaînées et replay golden ;
- [x] exploration de session, analyse de graphe et prévisualisation auteur ;
- [x] caractéristiques extensibles, effets différés, date logique et projections joueur ;
- [x] analyse d'entrée substituable et événements d'effets externes sans I/O.

## P0 — Configuration, RBAC et contexte d'établissement

Objectif : rendre la plateforme administrable sans valeurs métier codées en dur et établir le garde-fou d'autorisation utilisé par tous les lots suivants.

- [ ] registre de paramètres typés, versionnés et auditables avec défauts sûrs ;
- [ ] résolution hiérarchique documentée : plateforme → front/établissement → catégorie ou parcours → groupe/classe → utilisateur ;
- [ ] séparation stricte entre paramètres publiables et secrets d'infrastructure ;
- [ ] feature flags et activation de modules par front avec dépendances validées ;
- [ ] rôles configurables et permissions granulaires, policies serveur et endpoint des capacités effectives de l'utilisateur ;
- [ ] rôles de départ : administrateur plateforme, administrateur d'établissement, auteur, publieur, enseignant/formateur, apprenant et lecteur analytique ;
- [ ] audit des changements de configuration, rôles, permissions et affectations ;
- [ ] import/export d'une configuration portable, validée et versionnée ;
- [ ] modèle `Front` générique pouvant représenter une école, une entreprise ou une organisation de formation ;
- [ ] profil d'établissement : identité, branding, locale, fuseau horaire, terminologie, calendrier et politiques par défaut ;
- [ ] établissements, années/périodes scolaires, classes, groupes, inscriptions et liens enseignant–apprenant ;
- [ ] parcours et catégories de scénarios configurables, ordonnés et réutilisables ;
- [ ] affectation de scénarios ou parcours à une classe/groupe avec disponibilité et échéance ;
- [ ] politiques pédagogiques configurables : tentatives, reprise, aide autorisée, seuil de réussite et visibilité des résultats ;
- [ ] authentification configurable par front et mapping de claims externes vers rôles, sans rendre un OIDC obligatoire ;
- [ ] règles d'accès combinant front, rôle, groupe, catégorie, publication et feature flags ;
- [ ] tests d'isolation garantissant qu'un acteur d'un établissement ne lit ni ne modifie celui d'un autre.

Premier incrément attendu : ADR des frontières, registre + résolution de configuration, catalogue de permissions initial et squelette établissement/classe/catégorie, avant toute fonction IA.

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

## P3 — Expériences fonctionnelles augmentées

- [ ] dialogue contextuel du familier avec fallback déterministe ;
- [ ] analyse de réponse libre par rubrique, explication et confirmation ;
- [ ] copilote auteur : suggestions de nœuds, choix, indices et détection d'incohérences ;
- [ ] génération de quête en brouillon conforme au schéma, validée par le moteur et soumise à revue humaine ;
- [ ] interactions document et photo avec workflow de validation ;
- [ ] diff et restauration fonctionnelle de versions ;
- [ ] reporting d'usage de l'aide et de l'IA par établissement, classe, catégorie et période, agrégé et pseudonymisé.

## P4 — Approfondissement produit

- [ ] météo, présence et cycle jour/nuit comme contextes déterministes ;
- [ ] économie, récompenses et cosmétiques de familier ;
- [ ] packs de préconfiguration portables ;
- [ ] compétences, hauts faits, certification et analytics avancés ;
- [ ] notifications, LiveOps et espaces utilisateur configurables.

Ces lots ne doivent ni réimplémenter les règles narratives dans les clients, ni contourner le registre de configuration ou les policies RBAC.
