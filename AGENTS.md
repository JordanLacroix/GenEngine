# Instructions agents — GenEngine

**Ce fichier est la source unique des instructions du dépôt**, quel que soit l'agent
(Codex, Claude Code ou autre). `CLAUDE.md` n'y renvoie que par un lien : dupliquer
les consignes dans deux fichiers les fait diverger, et elles ont déjà donné des
ordres contradictoires sur la tâche à mener.

Lis ce fichier avant toute modification, puis consulte dans cet ordre :

1. [`specs/handoff.md`](specs/handoff.md) pour l’état courant et la prochaine tâche ;
2. [`specs/invariants.md`](specs/invariants.md) pour les règles non négociables ;
3. [`specs/architecture.md`](specs/architecture.md) pour les frontières DDD/Clean ;
4. [`specs/roadmap.md`](specs/roadmap.md), [`specs/functional-roadmap.md`](specs/functional-roadmap.md) et le fichier `tasks.md` du module concerné ;
5. [`specs/platform-configuration.md`](specs/platform-configuration.md) et [`specs/configuration-catalog.md`](specs/configuration-catalog.md) pour toute capacité configurable ;
6. les ADR applicables dans [`specs/adr/`](specs/adr/).

## Langue et communication

- Écris les échanges utilisateur et la documentation en français.
- Garde les noms de code, messages d’erreur techniques et commits en anglais.
- Annonce clairement toute hypothèse qui modifie le périmètre.
- Ne déclare jamais une tâche `done` avant implémentation et vérification réelles.

## Architecture non négociable

- GenEngine n’est pas un monolithe : `Authoring`, `Play`, `Identity`, `Configuration`, `PlayerExperience` et `Organization` sont des services autonomes ; tout nouveau bounded context exige un ADR avant création.
- Chaque service possède ses projets `Domain`, `Application`, `Infrastructure`, `Api` et sa base PostgreSQL.
- Aucun `ProjectReference`, accès SQL ou modèle de domaine ne traverse une frontière de service.
- `GenEngine.Narrative` reste un moteur métier pur, déterministe, sans I/O, réseau, base ou horloge implicite.
- `GenEngine.Observability` est un building block technique uniquement ; n’y ajoute aucune logique métier.
- N’ajoute ni bus, ni outbox, ni service supplémentaire sans consommateur ou besoin validé.
- Toute nouvelle dépendance sous `src/` doit être ajoutée à la liste blanche des tests d’architecture.

## Configuration et autorisation obligatoires

- Toute nouvelle fonctionnalité déclare dans la même PR ses paramètres typés, défauts, portées, validation et comportement désactivé.
- Toute nouvelle fonctionnalité ajoute ses permissions stables, policies serveur, rôles presets impactés et tests allow/deny.
- Les rôles sont personnalisables : le code métier teste des permissions et scopes, jamais un nom de rôle.
- L'isolation par front/organisation est appliquée côté service propriétaire et couverte par des tests.
- Les secrets restent hors du registre administrable et des exports ; seule une référence opaque peut être configurée.
- Toute mutation sensible de configuration, RBAC, IA, wallet ou publication est auditée sans donnée personnelle ni secret.
- IA et cloud restent optionnels : Docker, CI et parcours jouable disposent d'un fallback hors ligne.

## Méthode de travail

1. Pars de `main` à jour et crée une branche courte.
2. Implémente une tranche verticale cohérente ; sépare les PR de gouvernance, backend et clients lorsqu'elles vivent dans des dépôts distincts.
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
- Le document de scénario est **hashé canoniquement** et les sessions rejouent contre un snapshot publié. Tout champ ajouté au schéma doit être **nullable** : un type non-nullable sérialise sa valeur par défaut, change le hash de tous les snapshots existants et casse le replay.
- Une montée de schéma se prouve, elle ne s'affirme pas : reconstruis le moteur d'avant depuis git, calcule hash et état final rejoué avec ce binaire, et fige ces valeurs en tests.
- La validation d'une capacité se conditionne à sa **constante de schéma dédiée** (`InteractionsSchema`, `MediaSchema`, `OptionalInteractionsSchema`), jamais à `LatestSchema` : sinon monter la version invalide en silence tous les documents antérieurs.
- La CI **ne vérifie pas le format** et `main` porte des écarts préexistants (`FINALNEWLINE`, `WHITESPACE`). Ne les corrige pas au passage, mais n'en ajoute aucun.
- `scripts/smoke-test.sh` exige `GENENGINE_BOOTSTRAP_KEY` et une base Identity sans administrateur ; sur une base déjà amorcée il l'explique et sort en 1.
- Le contrôle de liens exclut les badges `img.shields.io` : leurs délais d'attente rendaient la CI non déterministe.

## Prochaine tâche

Le jalon 3 (durcissement) est clos. Le **jalon 4** est actif et centré sur la
plateforme configurable.

Sont livrés et fusionnés : le control plane `Configuration`, le service
`Organization` avec unités, périodes, memberships et imports de masse, la
configuration de référence **Le Diapason** (six postures, dix scénarios,
amorçage d'instance), les médias paramétrables (schéma v3), les interactions
facultatives (schéma v4), le pack d'assets CC0 servi par `Configuration`, et
côté clients la coque immersive, le Studio et la démonstration Diapason.

Restent ouverts, sans priorité arbitrée : la rotation quotidienne des scénarios
est documentée mais non implémentée ; le moteur ne distingue pas une partie
perdue d'une partie gagnée, donc les « game over » sont narratifs seulement ;
et aucune ambiance, musique ni illustration de personnage n'existe dans le
pack, ce qui laisse `visualUrl` inutilisé dans tout le contenu.

Les dix scénarios Diapason sont en schéma 6 et exploitent `document`,
`isOptional`, `consultedDocument`, `help` et le son du pack.

Consulte [`specs/handoff.md`](specs/handoff.md) avant de choisir : il fait foi
sur ce qui est réellement vérifié. Ne marque une tâche `done` qu'après code,
tests, contrats et documentation fusionnés.
