# Threat model initial

## Frontières de confiance

Les clients sont non fiables. Identity émet les JWT ; Authoring et Play les valident. Chaque service possède sa base. Play obtient un snapshot publié par un endpoint Authoring interne protégé par une clé distincte du JWT.

## Menaces et mitigations

| Menace | Mitigation initiale |
|---|---|
| Vol ou falsification d'identité | JWT signé, issuer/audience validés, secret injecté par configuration |
| Brute force | Rate limiting sur les endpoints d'authentification, hash de mot de passe ASP.NET |
| Modification d'un scénario publié | Snapshot immuable, hash canonique vérifié par Play |
| Double application d'une commande | Identifiant d'idempotence, réponse persistée, révision optimiste |
| Exécution de contenu auteur | Unions JSON fermées ; aucun script arbitraire |
| Accès croisé aux données | `ownerId` issu du token et contrôlé par les cas d'usage |
| Mouvement latéral entre services | Bases et identifiants séparés, aucun accès direct aux tables voisines |
| Secrets committés | `.env` ignoré, exemples factices, secret scanning GitHub |
| Déni de service | Limites de payload à compléter au jalon 3 ; rate limiting Identity déjà actif |

Les secrets de `compose.yaml` sont uniquement des valeurs locales. Tout environnement partagé doit les remplacer et terminer TLS au niveau de l'ingress.
