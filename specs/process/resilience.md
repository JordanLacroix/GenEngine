# Résilience interservices

Dernière mise à jour : 17 juillet 2026 (HRD-005).

## Portée

Le seul appel synchrone entre services est `Play → Authoring` :
`GET /internal/scenario-versions/{versionId}` (récupération d'un instantané de
version publiée, protégé par `X-Internal-Key`). Cet appel est **idempotent**, ce
qui rend un retry borné sûr.

## Politique appliquée

Le client HTTP typé `AuthoringSnapshotClient` (dans
`GenEngine.Play.Infrastructure`) est équipé du gestionnaire de résilience
standard `Microsoft.Extensions.Http.Resilience` (`AddStandardResilienceHandler`),
configuré par `PlayInfrastructureExtensions.ConfigureAuthoringResilience` :

| Étage | Réglage provisoire | Rôle |
|---|---|---|
| Timeout par tentative | 3 s | borne chaque essai |
| Timeout total | 12 s | borne l'opération complète, retries compris |
| Retry | 3 tentatives, backoff exponentiel + jitter | absorbe les défaillances transitoires (5xx, 408, erreurs réseau) |
| Circuit breaker | fenêtre 30 s, seuil 50 %, throughput min 10, ouverture 15 s | déleste quand Authoring est durablement dégradé |

Ces valeurs sont des **défauts de développement**, à réviser avec des SLO réels.
Le pipeline standard applique l'ordre : timeout total → retry → circuit breaker →
timeout par tentative.

## Pourquoi ces choix

- **Idempotence** : l'endpoint est un `GET` sans effet de bord ; rejouer est sûr.
- **Backoff + jitter** : évite les tempêtes de reprise synchronisées.
- **Circuit breaker** : au-delà d'un taux d'échec soutenu, on échoue vite plutôt
  que d'empiler des appels voués à échouer, laissant Authoring se rétablir.
- **Bibliothèque** : `Microsoft.Extensions.Http.Resilience` (Polly), maintenue
  par Microsoft, licence MIT — pas de réimplémentation maison.

Un refus d'authentification interne (`401`) n'est pas une défaillance transitoire
et n'est pas rejoué ; il est déjà audité côté Authoring
(`internal_snapshot_access_denied`, voir `specs/process/audit.md`).

## Procédure de test

`dotnet test tests/GenEngine.Play.IntegrationTests` couvre
(`AuthoringResilienceTests`) :

1. **Retry** avec la configuration de production : deux `503` suivis d'un `200`
   sont rejoués et l'appel finit en succès (trois tentatives observées).
2. **Circuit breaker** : sous des échecs soutenus, une `BrokenCircuitException`
   finit par être levée (circuit ouvert), avec une configuration rapide et sans
   délai pour un test déterministe.

Le parcours jouable complet (`scripts/smoke-test.sh`) continue de passer :
l'appel interne réel emprunte désormais le pipeline de résilience.

## Révision future

Recalibrer les seuils à partir des latences réelles d'Authoring et des SLO
(`specs/process/slo.md`). Étendre la politique à tout nouvel appel interservices
idempotent ; pour un appel non idempotent, restreindre ou supprimer le retry.
