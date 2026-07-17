# Audit métier

Dernière mise à jour : 17 juillet 2026 (HRD-004).

## Objectif

Rendre les opérations métier sensibles traçables — qui a fait quoi, sur quelle
ressource, avec quel résultat — **sans jamais** écrire de secret ni de donnée
personnelle dans les journaux.

## Où vit l'audit

- La primitive technique `IAuditLog` / `AuditEvent` vit dans le building block
  `GenEngine.Observability` (voir `src/BuildingBlocks/GenEngine.Observability/Auditing.cs`).
  Elle est **générique** : elle ne connaît aucun événement métier.
- Les événements sont émis à la **frontière Api** de chaque service (seule couche
  autorisée à référencer `GenEngine.Observability` par la liste blanche des tests
  d'architecture). Le nom et le sens de chaque événement sont donc décidés par le
  service concerné, pas par le building block.
- Les enregistrements passent par `ILogger`, donc suivent le même pipeline
  structuré (console JSON + OTLP) que le reste de la plateforme et arrivent dans
  Loki via le Collector.

## Champs d'un enregistrement

| Champ | Contenu | Règle |
|---|---|---|
| `action` | nom stable, ex. `scenario_published` | jamais de texte libre |
| `outcome` | `Success`, `Failure` ou `Denied` | niveau `Warning` si non `Success` |
| `actor_id` | identifiant de l'acteur (GUID) | jamais un nom, e-mail ou token |
| `resource_type` / `resource_id` | ressource affectée | identifiants uniquement |
| `properties.*` | propriétés non sensibles | dény-list appliquée à l'écriture |

Chaque entrée porte un scope `audit=true`, filtrable dans Loki.

## Politique de non-fuite

- **Interdits** dans un audit : mot de passe, hash, token/JWT, clé d'API ou
  interne, e-mail, nom d'utilisateur, corps de requête brut (scénario, état de
  session).
- **Autorisés** : identifiants opaques (GUID), noms d'action, résultats, numéros
  de version, indicateurs booléens (`replayed`).
- Défense en profondeur : `AuditLog` **supprime** toute propriété dont la clé
  contient un fragment sensible (`password`, `secret`, `token`, `authorization`,
  `credential`, `apikey`, `hash`, `email`/`mail`, …) et compte les suppressions
  dans `audit.dropped_properties`. Les appelants restent responsables de ne
  jamais transmettre de tels contenus.
- L'audit ne doit jamais faire échouer le flux métier ; il n'y a pas d'I/O propre
  au-delà du logger.

## Catalogue des événements audités

| Service | Action | Déclencheur | Résultat(s) |
|---|---|---|---|
| Identity | `user_registered` | inscription réussie | Success |
| Identity | `login_succeeded` | connexion réussie | Success |
| Identity | `login_failed` | identifiants invalides | Failure |
| Authoring | `scenario_imported` | import de scénario | Success |
| Authoring | `scenario_validated` | validation de brouillon | Success |
| Authoring | `scenario_published` | publication de version | Success (+ `version_id`, `version_number`) |
| Authoring | `internal_snapshot_access_denied` | clé interne invalide sur `/internal/scenario-versions/{id}` | Denied |
| Play | `session_started` | démarrage de session | Success (+ `scenario_version_id`) |
| Play | `choice_submitted` / `choice_replayed` | soumission de commande (rejeu idempotent) | Success (+ `command_id`) |
| Play | `session_paused` | mise en pause | Success |
| Play | `session_resumed` | reprise | Success |

`login_succeeded` ne porte volontairement pas d'`actor_id` : l'endpoint de
connexion n'expose pas l'identifiant utilisateur et l'auditer imposerait de le
dériver du nom d'utilisateur (donnée personnelle). `login_failed` n'enregistre
aucun identifiant de tentative pour la même raison ; le signal d'échec suffit à
détecter le forçage brut par corrélation temporelle.

## Consulter les journaux d'audit

Dans Grafana → Explore → source Loki :

```logql
{service_name=~"genengine-.*"} | json | audit="true"
```

Filtrer un type d'événement : `| audit_action="login_failed"`.

## Procédure de test

1. **Unitaire** — `dotnet test tests/GenEngine.Services.Tests` couvre l'émission
   (action, outcome, scope `audit=true`) et la suppression des propriétés
   sensibles (`AuditLogTests`).
2. **Bout en bout** — stack complète active, exécuter un parcours puis vérifier
   dans Loki que les événements ci-dessus apparaissent avec `audit=true` et
   **sans** mot de passe, token ni e-mail.

## Révision future

Étendre le catalogue si de nouvelles opérations sensibles apparaissent ; ne
jamais ajouter de champ personnel. Une rétention et une protection en écriture
des journaux d'audit relèveront d'une étape ultérieure de durcissement.
