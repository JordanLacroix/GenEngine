# Contrats HTTP

Toutes les API exposent `GET /health/live` et `GET /health/ready`. Les erreurs utilisent Problem Details. Les routes métier exigent un JWT Bearer sauf inscription, connexion, catalogue public et contrat interne explicitement protégé.

## Identity — port 5203

- `POST /auth/register`
- `POST /auth/login`

## Authoring — port 5201

- `GET /catalog?limit=20` — dernières versions publiées, accès public, limite bornée entre 1 et 100
- `POST /scenarios/import`
- `GET /scenarios/{id}`
- `PUT /scenarios/{id}/draft`
- `POST /scenarios/{id}/validate`
- `POST /scenarios/{id}/publish`
- `GET /scenarios/{id}/versions`
- `GET /internal/scenario-versions/{versionId}` — clé interservice

## Play — port 5202

- `POST /sessions`
- `GET /sessions/{id}`
- `GET /sessions/{id}/current-step`
- `POST /sessions/{id}/inputs`
- `POST /sessions/{id}/continue` — progression d'une interaction de narration, commande idempotente
- `POST /sessions/{id}/answers` — soumission d'une réponse de quiz, commande idempotente
- `POST /sessions/{id}/pause`
- `POST /sessions/{id}/resume`

L'OpenAPI généré par chaque service reste la source de vérité exécutable.
