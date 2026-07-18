# Tâches — Identity

| ID | Statut | Tâche |
|---|---|---|
| IDN-001 | done | Inscrire un compte local avec mot de passe hashé |
| IDN-002 | done | Authentifier et émettre un JWT à issuer/audience contrôlés |
| IDN-003 | done | Persister les comptes dans une base PostgreSQL dédiée |
| IDN-004 | done | Appliquer un rate limiting aux endpoints d'authentification |
| IDN-005 | done | Définir le catalogue stable de permissions et les presets de rôles initiaux |
| IDN-006 | in-progress | Administrer rôles et permissions avec audit et policies serveur |
| IDN-007 | done | Exposer les permissions effectives de l'utilisateur authentifié |
| IDN-008 | in-progress | Porter les affectations de rôles par identifiant de front sans posséder classes ou groupes |
| IDN-009 | todo | Tester systématiquement allow/deny et scopes de front dans les policies |
| IDN-010 | todo | Créer, cloner, versionner et archiver des rôles custom composés de permissions stables |
| IDN-011 | in-progress | Gérer les affectations de rôles portées et temporisées avec provenance explicable |
| IDN-012 | todo | Empêcher auto-élévation, délégation hors scope et suppression du dernier administrateur |
| IDN-013 | done | Rechercher et paginer les utilisateurs, consulter leurs rôles et leur état |
| IDN-014 | done | Activer, désactiver et supprimer logiquement un compte avec garde-fou du dernier détenteur de `rbac.manage` |
| IDN-015 | done | Supprimer un rôle custom et retirer explicitement une affectation de rôle |
