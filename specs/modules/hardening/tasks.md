# Tâches — Durcissement

| ID | Statut | Tâche | Critère d’acceptation |
|---|---|---|---|
| HRD-001 | done | Instrumenter les trois API avec OpenTelemetry | Logs structurés, traces HTTP, appels sortants, métriques HTTP et runtime exportés en OTLP |
| HRD-002 | done | Fournir une stack d’observabilité locale | Grafana, Prometheus, Tempo, Loki et Collector démarrent avec une surcouche Compose documentée |
| HRD-003 | todo | Définir les SLI, SLO et alertes initiales | Disponibilité, latence et erreurs disposent de seuils justifiés et testables |
| HRD-004 | todo | Renforcer l’audit métier | Événements sensibles traçables sans secret ni donnée personnelle dans les journaux |
| HRD-005 | todo | Ajouter les politiques de résilience interservices | Timeouts, retry borné et circuit breaker vérifiés pour les appels idempotents |
| HRD-006 | todo | Automatiser sauvegarde et restauration | Chaque base possède une procédure chiffrée, testée et documentée |
| HRD-007 | todo | Ajouter une outbox si un consommateur asynchrone existe | Aucun bus ni outbox sans besoin consommateur validé |
