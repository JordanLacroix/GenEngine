# SLI, SLO et budget d'erreur

Dernière mise à jour : 16 juillet 2026 (HRD-003).

## Statut des objectifs

> **Objectifs provisoires de développement — non contractuels.**
> GenEngine n'a pas encore de trafic réel ni d'attentes produit validées. Les
> seuils ci-dessous sont des repères de développement destinés à valider
> l'outillage d'alerte, **pas** des engagements de production. Ils devront être
> révisés à partir de données de trafic réelles et d'exigences produit avant
> tout engagement de niveau de service.

## Périmètre

- Les SLI portent sur le trafic **utilisateur** HTTP des trois API `Authoring`,
  `Play` et `Identity`.
- Les routes de sonde `/health/live` et `/health/ready` sont **exclues** de tous
  les calculs (filtre `http_route!~"/health/.*"`). Ce sont des vérifications
  internes, pas du trafic utilisateur.
- Source des mesures : métriques OpenTelemetry exportées par chaque API et
  scrappées par Prometheus, série `http_server_request_duration_seconds_*`
  (labels `service_name`, `http_route`, `http_response_status_code`).

## Indicateurs (SLI)

| SLI | Définition | Expression de base |
|---|---|---|
| Disponibilité | Part des requêtes utilisateur sans erreur serveur (5xx) | `1 - (rate(5xx) / rate(total))` |
| Taux d'erreurs | Part des requêtes utilisateur en 5xx | `rate(5xx) / rate(total)` |
| Latence | 95e centile de la durée des requêtes utilisateur | `histogram_quantile(0.95, rate(bucket))` |

Les erreurs 4xx (entrées invalides, non-authentifié) ne comptent pas contre la
disponibilité : ce sont des réponses attendues, pas des pannes du service.

## Objectifs provisoires (SLO)

| Objectif | Cible provisoire | Fenêtre |
|---|---|---|
| Disponibilité | 99,0 % | 30 jours glissants |
| Latence p95 | < 500 ms | 5 min |

Budget d'erreur de disponibilité = `1 - 0,99 = 1 %` des requêtes sur 30 jours.

## Règles Prometheus

Versionnées sous `deploy/observability/rules/`, montées en lecture seule dans le
conteneur Prometheus (`/etc/prometheus/rules`) et chargées via `rule_files` dans
`prometheus.yaml`.

### Recording rules — `slo-recording.rules.yaml`

Précalculent, par service, le débit de requêtes, le débit d'erreurs 5xx, le
ratio d'erreurs sur plusieurs fenêtres (5 min, 30 min, 1 h, 6 h), la
disponibilité et la latence p95. Un ratio sans trafic produit `NaN` : aucune
alerte ne se déclenche alors, ce qui est le comportement voulu.

### Alerting rules — `slo-alerts.rules.yaml`

Alertes multi-fenêtres sur le taux de consommation du budget d'erreur (méthode
du *SRE Workbook*), calées sur le SLO provisoire de 99 % :

| Alerte | Condition | Sévérité | Sens |
|---|---|---|---|
| `GenEngineErrorBudgetBurnFast` | ratio 5xx > 14,4 % sur 5 min **et** 1 h | critical | épuise ~2 % du budget mensuel en 1 h |
| `GenEngineErrorBudgetBurnSlow` | ratio 5xx > 6 % sur 30 min **et** 6 h | warning | dérive soutenue |
| `GenEngineHighLatencyP95` | p95 > 500 ms sur 10 min | warning | dégradation de latence |
| `GenEngineServiceNoTraffic` | aucune série (santé comprise) pour un service sur 10 min | critical | service muet : pire cas de disponibilité, invisible pour le burn |
| `GenEngineMetricsTargetDown` | `up{job="otel-collector"} == 0` 5 min | critical | SLI aveugles |

`GenEngineServiceNoTraffic` couvre le trou du burn : un service arrêté ne produit
plus d'erreurs, donc son ratio d'erreurs est `NaN` et n'alerte pas. Les sondes de
santé servent de battement de cœur ; leur disparition révèle le service muet. Une
règle par service (pas de templating de liste dans Prometheus).

Seuils de burn : `14,4 = 2 % de budget en 1 h` et `6 = 5 % de budget en 6 h`
pour un budget de 1 %. Ces multiplicateurs sont provisoires et suivront toute
révision du SLO.

## Visualisation

Dashboard Grafana `GenEngine — SLO et budget d'erreur`
(`deploy/observability/grafana/dashboards/genengine-slo.json`, uid
`genengine-slo`) : disponibilité, taux d'erreurs avec seuils de burn, latence
p95 et budget d'erreur restant sur 30 jours, par service. Accès via Grafana sur
`http://localhost:3000` (Dashboards ou Explore).

## Procédure de test

Prérequis : stack complète active
(`docker compose -f compose.yaml -f compose.observability.yaml up --build -d --wait`).

1. **Validation syntaxique** des règles, sans dépendre d'un conteneur en cours :

   ```bash
   docker run --rm -v "$(pwd)/deploy/observability/rules:/rules:ro" \
     --entrypoint promtool prom/prometheus:v3.12.0 \
     check rules /rules/slo-recording.rules.yaml /rules/slo-alerts.rules.yaml
   ```

   Attendu : `SUCCESS` pour chaque fichier.

2. **Chargement effectif** dans Prometheus :

   ```bash
   curl -s http://localhost:9090/api/v1/rules | \
     python3 -c "import sys,json;[print(g['name']) for g in json.load(sys.stdin)['data']['groups']]"
   ```

   Attendu : `genengine_sli_recording` et `genengine_slo_alerts`.

3. **Santé des règles** (expressions valides sur métriques réelles) :

   ```bash
   curl -s 'http://localhost:9090/api/v1/rules?type=alert' | \
     python3 -c "import sys,json;[print(r['name'],r['health']) for g in json.load(sys.stdin)['data']['groups'] for r in g['rules']]"
   ```

   Attendu : `health = ok` pour chaque alerte.

4. **Démonstration sur trafic réel** : générer du trafic utilisateur puis vérifier
   que les séries enregistrées sont peuplées, par exemple

   ```bash
   curl -s --data-urlencode 'query=service:http_requests:rate5m' \
     http://localhost:9090/api/v1/query
   ```

   Attendu : valeurs par `service_name`, exclusion effective de `/health/*`.

5. **Exclusion des health checks** : confirmer qu'aucune route `/health/*`
   n'entre dans les numérateurs (le filtre `http_route!~"/health/.*"` retourne un
   ensemble vide pour ces routes).

## Révision future

À faire dès qu'un trafic représentatif et des attentes produit existent :
recalculer les cibles à partir des distributions réelles, distinguer les objectifs
par route critique si nécessaire, et acter tout engagement de production via un ADR.
