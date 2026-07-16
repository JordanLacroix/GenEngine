# Instructions Claude Code — GenEngine

Lis ce fichier avant toute modification, puis consulte dans cet ordre :

1. [`specs/handoff.md`](specs/handoff.md) pour l’état courant et la prochaine tâche ;
2. [`specs/invariants.md`](specs/invariants.md) pour les règles non négociables ;
3. [`specs/architecture.md`](specs/architecture.md) pour les frontières DDD/Clean ;
4. [`specs/roadmap.md`](specs/roadmap.md) et le fichier `tasks.md` du module concerné ;
5. les ADR applicables dans [`specs/adr/`](specs/adr/).

## Langue et communication

- Écris les échanges utilisateur et la documentation en français.
- Garde les noms de code, messages d’erreur techniques et commits en anglais.
- Annonce clairement toute hypothèse qui modifie le périmètre.
- Ne déclare jamais une tâche `done` avant implémentation et vérification réelles.

## Architecture non négociable

- GenEngine n’est pas un monolithe : `Authoring`, `Play` et `Identity` sont trois services autonomes.
- Chaque service possède ses projets `Domain`, `Application`, `Infrastructure`, `Api` et sa base PostgreSQL.
- Aucun `ProjectReference`, accès SQL ou modèle de domaine ne traverse une frontière de service.
- `GenEngine.Narrative` reste un moteur métier pur, déterministe, sans I/O, réseau, base ou horloge implicite.
- `GenEngine.Observability` est un building block technique uniquement ; n’y ajoute aucune logique métier.
- N’ajoute ni bus, ni outbox, ni service supplémentaire sans consommateur ou besoin validé.
- Toute nouvelle dépendance sous `src/` doit être ajoutée à la liste blanche des tests d’architecture.

## Méthode de travail

1. Pars de `main` à jour et crée une branche courte.
2. Implémente une seule préoccupation cohérente.
3. Mets à jour code, tests, README, specs et tâches dans la même PR.
4. Utilise les versions centralisées dans `Directory.Packages.props` et conserve les `packages.lock.json`.
5. Utilise des commits conventionnels et le modèle de pull request du dépôt.
6. Ne fusionne qu’après réussite de tous les contrôles GitHub requis.

## Vérifications minimales

```bash
dotnet restore GenEngine.sln --locked-mode
dotnet build GenEngine.sln --no-restore -warnaserror
dotnet test GenEngine.sln --no-build
dotnet format GenEngine.sln --no-restore --verify-no-changes
dotnet list GenEngine.sln package --vulnerable --include-transitive
docker compose -f compose.yaml -f compose.observability.yaml config --quiet
```

Pour un changement affectant le déploiement ou le parcours jouable :

```bash
docker compose -f compose.yaml -f compose.observability.yaml up --build --detach --wait
./scripts/smoke-test.sh
```

## Pièges connus

- Les fichiers C# suivent `insert_final_newline = false` dans `.editorconfig` ; lance `dotnet format`.
- `localhost:3100` (Loki) et `localhost:3200` (Tempo) sont des API, pas des interfaces graphiques.
- Consulte logs et traces dans Grafana sur `localhost:3000`, via **Explore**.
- Grafana autorise l’accès anonyme en lecture uniquement pour le développement local.
- Le smoke test Compose standard ne démarre pas la surcouche d’observabilité ; la CI valide néanmoins sa configuration.
- Les identifiants et secrets présents dans Compose sont exclusivement des valeurs de développement local.
- Le projet est public mais ne possède pas encore de licence ; n’affirme aucune permission de réutilisation.

## Prochaine tâche

Commence par `HRD-005`, décrite dans [`specs/modules/hardening/tasks.md`](specs/modules/hardening/tasks.md). Ne poursuis pas automatiquement vers `HRD-006` tant que `HRD-005` n’est pas fusionnée et documentée.
