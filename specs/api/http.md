# Contrats HTTP

Toutes les API exposent `GET /health/live` et `GET /health/ready`. Les erreurs utilisent Problem Details. Les routes métier exigent un JWT Bearer sauf inscription, connexion, catalogue public et contrat interne explicitement protégé.

## Identity — port 5203

- `POST /auth/register`
- `POST /auth/login`

## Authoring — port 5201

- `GET /catalog?limit=20` — dernières versions publiées, accès public, limite bornée entre 1 et 100
- `POST /scenarios/import` — migre le brouillon vers le schéma courant avant stockage
- `GET /scenarios/{id}`
- `PUT /scenarios/{id}/draft` — migre le brouillon vers le schéma courant avant stockage
- `POST /scenarios/{id}/validate`
- `POST /scenarios/{id}/analyze` — boucles, sorties garanties, risques d'impasse conditionnelle et fins inatteignables
- `POST /scenarios/{id}/preview` — prévisualisation depuis un nœud et un tour choisis avec état joueur injecté
- `POST /scenarios/{id}/publish`
- `GET /scenarios/{id}/versions`
- `GET /internal/scenario-versions/{versionId}` — clé interservice

## Play — port 5202

- `POST /sessions`
- `GET /sessions/{id}`
- `GET /sessions/{id}/current-step`
- `GET /sessions/{id}/tree` — arbre complet avec état courant, visité, inexploré ou verrouillé et explication des conditions
- `POST /sessions/{id}/inputs`
- `POST /sessions/{id}/continue` — progression d'une interaction de narration, commande idempotente
- `POST /sessions/{id}/answers` — soumission d'une réponse de quiz, commande idempotente
- `POST /sessions/{id}/text-inputs` — soumission idempotente d'un texte libre ; produit une analyse sans faire progresser le tour
- `POST /sessions/{id}/text-inputs/confirm` — confirme l'analyse et progresse, ou la refuse et revient à la saisie
- `POST /sessions/{id}/pause`
- `POST /sessions/{id}/resume`

L'OpenAPI généré par chaque service reste la source de vérité exécutable.
