# ADR 0005 — Control plane de configuration distribué

- Statut : accepté
- Date : 18 juillet 2026

## Contexte

GenEngine doit servir des écoles, entreprises, organismes de formation et fronts custom. Le jeu global, l'histoire, les catégories, l'authentification, les providers IA, les familiers, l'économie et l'activation des modules doivent être configurables sans transformer Authoring ou Identity en monolithes.

## Décision

Un service autonome `Configuration` possède le registre typé et les versions publiées de l'expérience d'un front. Il expose une vue publique sans secret et une surface d'administration protégée par permissions.

- `Configuration` possède les paramètres et leur résolution, pas les comportements des autres domaines.
- `Authoring` possède les scénarios et consomme le contexte jeu/histoire/catégorie pour générer un brouillon.
- `Identity` possède utilisateurs, rôles custom, permissions et providers d'authentification ; la configuration publiée choisit les modes disponibles.
- `Play` fige les paramètres effectifs nécessaires dans une session.
- `Assistant` et `Economy` posséderont respectivement l'exécution IA/familiers et les wallets/achats ; ils consommeront les paramètres publiés.

Les services conservent leurs bases et contrats. Aucun accès SQL ou `ProjectReference` interservice n'est introduit.

## Conséquences

- l'administration est une application distincte du Studio dans les clients ;
- une version publiée est immuable et les modifications utilisent un contrôle optimiste ;
- les secrets sont des références opaques et ne figurent pas dans la vue publique ;
- un front dispose d'une configuration de démonstration complète sans cloud ;
- le provider Foundry suit l'API OpenAI `/openai/v1`, l'ancien Azure AI Inference SDK étant proche de sa fin de vie en juillet 2026 ;
- l'ajout des services `Assistant` et `Economy` demandera un ADR de frontière complémentaire au moment de leur premier comportement persistant.
