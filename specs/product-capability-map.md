# Carte des capacités produit

Cette carte recolle la réalisation au plan produit initial. Les statuts évitent de confondre fondation technique, contrat partiel et fonctionnalité réellement exploitable.

| Domaine | Statut | Réellement disponible | Prochain incrément fonctionnel |
|---|---|---|---|
| Narrative | utilisable | moteur déterministe, conditions/effets, interactions typées, replay, migrations, pause/reprise, arbre | compléter les effets de déblocage, titres, compétences et médias |
| Authoring / Studio | partiel | import, validation, analyse, aperçu, publication et génération contextualisée | édition graphique, diff/restauration, coédition et régénération de branche |
| Configuration / Branding | partiel | jeu, histoire globale, locale, fuseau, catégories, modules, vocabulaire et copies arbitraires | couleurs, logos, typographies, assets et héritage par portée |
| Identity / RBAC | partiel | comptes locaux, Entra, modes cumulatifs, rôles custom, permissions et scopes | gestion des utilisateurs, reset/confirmation, claims→rôles et audit complet |
| Tenancy / Organisations | partiel | types d’organisation et unités hiérarchiques | memberships, encadrants, isolation multi-front et politiques héritées |
| Catalog | partiel | catalogue public et catégories | Journey/Parcours, N-N catégories, planification, optionnels et règles de déblocage |
| Assistant / Familier | partiel | définitions, choix et personnalisation persistée | aide contextuelle réelle, fallback auteur, snapshot de session, assets et import Codex Pets |
| Economy / Shop | partiel | monnaie, ledger, récompenses narratives, offres, achats et possessions | types extensibles, inventaire/équipement, stock, limites, bundles et compensation |
| IA / Providers | partiel | Offline déterministe et premier Azure AI Foundry pour la génération | profils routables, double avis, assistant, analyse, garde-fous, quotas et coûts |
| Groups / Assignments | absent | unités seulement | membres, encadrants, affectations, échéances, suivi et relances |
| Metering | absent | — | ledger tokens/coûts, pricing historisé, quotas et alertes |
| Pedagogy / Journal | fondation | projection joueur et événements narratifs | dimensions, rubriques, événements notables et journal filtrable |
| Insights | absent | — | événements de progression, KPI configurables, cohortes et exports |
| Media | absent | — | photos, documents, stockage local et validation |
| Packs | absent | — | manifeste ouvert, import, validation, aperçu, application et export |
| Help / Onboarding | absent | — | centre d’aide, glossaire et tutoriel déclaratif persistant |
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
