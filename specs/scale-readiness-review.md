# Revue fonctionnelle de préparation à l’échelle

Date de référence : 18 juillet 2026. Cette revue compare `main` et la tranche `feat/immersive-player-experience` au plan produit initial.

## Positionnement

GenEngine n’est plus un POC ni un prototype produit. C’est une plateforme narrative en construction, composée de services indépendants, de clients Web/iOS et d’un socle fonctionnel déjà exécutable. Le terme correct dans les interfaces et la documentation courante est **plateforme produit** ou **socle opérationnel**, selon le contexte.

Cela ne signifie pas que la plateforme est déjà prête pour un déploiement multi-organisation à grande échelle. Le moteur narratif et le parcours individuel sont solides ; les fonctions d’exploitation collective, d’isolation, de gouvernance et de pilotage restent incomplètes.

## Capacités réellement exploitables

- moteur déterministe, interactions typées, effets différés, sauvegardes, replay, pause/reprise et arbre de session ;
- authoring avec import, recherche, validation, analyse, aperçu, génération de brouillon, publication, versions et archivage ;
- comptes locaux et Entra, utilisateurs, rôles personnalisés, permissions, affectations portées et suppression logique ;
- configuration versionnée par `frontId`, vocabulaire du jeu, jeu/histoire, catégories, parcours, unités, modules, familier et économie ;
- expérience joueur individuelle : intro, compte, familier, onboarding persistant, carte, recherche, journal, maîtrise, magasin et aide ;
- Azure AI Foundry pour la génération de scénarios lorsque l’infrastructure est configurée, avec générateur offline disponible ;
- Docker, observabilité, audit, sauvegardes et CI de sécurité.

## Bloquants fonctionnels avant déploiement multi-organisation

| Priorité | Manque | Constat actuel | Critère de sortie |
|---|---|---|---|
| P0 | Contexte de front autoritatif | plusieurs appels et projections utilisent encore `frontId=default`; le client choisit parfois lui-même le front | front issu du jeton/domaine/affectation, propagé entre services et impossible à usurper |
| P0 | Isolation multi-tenant | aucune preuve systématique qu’un acteur d’une organisation ne peut lire ou modifier les ressources d’une autre | filtres et policies par ressource, tests d’isolation croisés sur les cinq services |
| P0 | Memberships et encadrement | les unités sont configurables, mais les membres, responsables et périodes ne forment pas encore un workflow métier complet | CRUD memberships, encadrants, import en masse, historique et écrans Web/iOS |
| P0 | Affectations exécutées | les affectations sont décrites dans la configuration, mais ne filtrent ni n’autorisent réellement le catalogue/runtime | affecter parcours/scénario à un groupe ou joueur, fenêtres/échéances, statut et contrôle serveur |
| P0 | Scopes RBAC appliqués aux ressources | les permissions et scopes existent, mais l’autorisation reste principalement fondée sur les permissions du JWT | résolution permission + scope + ressource dans chaque endpoint sensible, allow/deny testés |
| P0 | Cycle de compte complet | pas de reset mot de passe, confirmation e-mail, MFA, révocation des sessions ni mapping claims Entra→rôles | parcours administrateur et utilisateur complets, récupération sécurisée et révocation immédiate |
| P0 | Catalogue publié cohérent | parcours/catégories/scénarios sont configurables mais la projection Play envoie encore des identifiants de parcours/catégorie incomplets | relations publiées/versionnées, snapshot de session, progression exacte par parcours et catégorie |
| P0 | Exploitation de masse | les écrans recherchent les utilisateurs/scénarios, mais pas d’import/export, opérations en lot ou traitement asynchrone suivi | CSV/import, actions bulk, validation, aperçu, rapport d’erreurs et idempotence |

## Fonctions importantes encore partielles

| Domaine | Disponible | Manque pour la cible |
|---|---|---|
| Familier/assistant | personnalisation, assets, fréquence stockée, aide déterministe à la demande | interventions proactives en jeu, snapshot figé dans la session, contexte borné, conversation, indices auteur complets, import Codex Pets |
| IA | génération offline/Foundry d’un brouillon validé | profils routables, test de connexion, fallback automatique, analyse libre IA, assistant IA, double avis, redaction, modération, prompts versionnés |
| Metering IA | permissions et configuration préparatoire | ledger tokens/coûts, pricing historisé, quotas par portée, alertes et dégradation offline |
| Économie | monnaie, ledger, récompenses, offres, achats et possessions | inventaire/équipement, types extensibles, stock, limites, ciblage, bundles, compensation et ajustement audité |
| Journal/progression | événements persistants, choix/nœuds/fins, maîtrise, filtres API | filtres UI, export, événements notables auteur, conséquences, synthèse par parcours et recalcul |
| Arbre/rejouabilité | arbre de session et maîtrise cross-session | écran de fin systématique avec arbre complet, comparaison du chemin, branches verrouillées expliquées et guidage du familier sur chemins connus |
| Aide/onboarding | articles, glossaire, aide contextuelle et tutoriel persistant | recherche, ciblage par écran/nœud/step, analytics, localisation et édition riche |
| Branding | copies, terminologie et quelques assets HTTPS | design tokens complets, logos/favicons/typographies gérés, héritage par portée et prévisualisation avant publication |
| Studio | graphe Web, édition de scène, validation, aperçu et publication | création/liaison visuelle complète de nœuds et steps, diff/restauration, coédition, commentaires/review et régénération ciblée |
| Démonstration | parcours offline distinct et cible de durée documentée | validation automatisée de durée/contenu et stricte parité des comportements avec le backend |

## Domaines encore absents comme parcours bout en bout

- médias et documents : upload, stockage, scan, rétention, validation photo/document ;
- Insights : KPI configurables, cohortes, progression encadrant, exports et anonymisation ;
- compétences, hauts faits, titres équipables et certificats vérifiables ;
- notifications in-app/e-mail, relances d’échéance et préférences ;
- présence agrégée temps réel et mises à jour SignalR ;
- packs de configuration importables/exportables et réversibles ;
- espace personnel Builder et widgets par rôle/front ;
- gouvernance : consentements, export/effacement RGPD, rétention et modération ;
- surveys, NPS et questionnaires pré/post ;
- LiveOps : saisons, événements, boutique temporaire et cadence ;
- StoryArc exécuté : continuité, jalons et déblocage inter-scénarios ;
- intégrations LMS/LTI/xAPI, e-mail opérationnel et webhooks.

## Ordre recommandé des prochains lots

1. **Organisation et isolation** : contexte de front, memberships, encadrants, scopes ressource et tests d’isolation.
2. **Parcours et affectations** : catalogue publié versionné, règles d’accès, échéances, progression de groupe et écrans associés.
3. **Assistant réellement actif** : snapshot de session, indices auteur, interventions proactives et écran de fin/arbre/rejouabilité.
4. **IA maîtrisée** : profils/provider routing, assistant Foundry, analyse libre, metering, pricing, quotas et fallback.
5. **Pilotage** : événements de progression complets, Insights, cohortes, exports et journal enrichi.
6. **Économie avancée et médias**, puis Packs, Notifications/Realtime, Governance et LiveOps.

Chaque lot doit rester vertical : modèle et persistance propriétaires, API, RBAC/scopes, configuration, audit, tests d’isolation, Web, iOS et documentation dans le même incrément.
