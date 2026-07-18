# Carte des capacités produit

Cette carte recolle la réalisation au plan produit initial. Les statuts évitent de confondre fondation technique, contrat partiel et fonctionnalité réellement exploitable.

Les blocages transverses, critères de sortie et lots recommandés sont détaillés dans la [`revue fonctionnelle de préparation à l’échelle`](scale-readiness-review.md).

| Domaine | Statut | Réellement disponible | Prochain incrément fonctionnel |
|---|---|---|---|
| Narrative | utilisable | moteur déterministe, conditions/effets, interactions typées, replay, migrations, pause/reprise, arbre | compléter les effets de déblocage, titres, compétences et médias |
| Authoring / Studio | utilisable | recherche paginée, génération contextualisée, édition graphique Web et édition de scène iOS, validation, aperçu, publication et archivage | édition de graphe native complète, diff/restauration, coédition et régénération de branche |
| Configuration / Branding | partiel | jeu, histoire globale, locale, fuseau, catégories, modules, vocabulaire et copies arbitraires | couleurs, logos, typographies, assets et héritage par portée |
| Identity / RBAC | utilisable en mono-front | recherche paginée des comptes, activation, suppression logique, Entra/local, rôles custom supprimables, permissions, scopes et garde-fou du dernier administrateur | reset/confirmation/MFA, révocation de sessions, claims→rôles, contrôle de portée sur chaque ressource et audit consultable |
| Tenancy / Organisations | utilisable | types d’organisation, unités hiérarchiques, périodes, memberships/encadrants historisés et import de masse prévalidé | contexte de front autoritatif sur tous les services, export de masse, isolation multi-front systématique et politiques héritées |
| Catalog | utilisable pour les affectations | catalogue public, catégories, parcours N-N, rattachement de scénarios, affectations scénario/catégorie/parcours et catalogues clients filtrés | optionnels, prérequis, règles de déblocage et progression collective |
| Assistant / Familier | partiel | définitions, assets HTTPS avec licence, aperçu, choix et personnalisation persistée, aide contextuelle déterministe à la demande | interventions réellement déclenchées pendant une session, snapshot de session, indices auteurs complets, conversation IA et import Codex Pets |
| Economy / Shop | partiel | monnaie, ledger, récompenses narratives, offres, achats et possessions | types extensibles, inventaire/équipement, stock, limites, bundles et compensation |
| IA / Providers | partiel | Offline déterministe et premier Azure AI Foundry pour la génération | profils routables, double avis, assistant, analyse, garde-fous, quotas et coûts |
| Groups / Assignments | utilisable | unités, périodes, membres, encadrants et affectations scénario/catégorie/parcours avec fenêtres, échéances et contrôle runtime | ciblage joueur direct, suivi collectif, relances et export |
| Metering | absent | — | ledger tokens/coûts, pricing historisé, quotas et alertes |
| Pedagogy / Journal | partiel | projection joueur persistante, événements de jeu, maîtrise cross-session et API de journal filtrable | filtres dans les clients, export, conséquences visibles, dimensions, rubriques et événements notables auteur |
| Insights | absent | — | événements de progression, KPI configurables, cohortes et exports |
| Media | absent | — | photos, documents, stockage local et validation |
| Packs | absent | — | manifeste ouvert, import, validation, aperçu, application et export |
| Help / Onboarding | utilisable | centre d’aide, glossaire, aide contextuelle déterministe et tutoriel déclaratif/versionné persistant | recherche plein texte, ciblage écran/nœud/step, analytics d’usage, localisation complète et aide conversationnelle optionnelle |
| Builder | absent | — | espace utilisateur, widgets et défauts par rôle/front |
| Competency | absent | — | référentiels, maîtrise, hauts faits et certificats |
| Notifications / Realtime | absent | — | notifications, préférences et présence agrégée SignalR |
| Governance / Survey / LiveOps | absent | — | consentement/RGPD, modération, enquêtes, saisons et événements |
| StoryArc | fondation | histoire globale injectée dans la génération | continuité inter-scénarios, jalons et cadence configurée |

## Ordre fonctionnel retenu

1. terminer Configuration/Branding et les portées de configuration ;
2. livrer Groups + memberships + encadrants + isolation scoped ;
3. construire les vrais Parcours et Assignments ;
4. rendre le familier utile en jeu avec une aide hors ligne avant l’IA ;
5. ajouter profils IA, metering et quotas ;
6. approfondir Economy, puis Packs ;
7. dérouler Pedagogy/Journal, Insights, Media, Help, Builder et les domaines d’exploitation.

Chaque incrément doit être livré verticalement dans le backend, Web et iOS, avec RBAC et configuration dans la même PR fonctionnelle.
