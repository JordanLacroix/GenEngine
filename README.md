# GenEngine

Backend narratif autoritatif en .NET 10 LTS.

## État

Le dépôt est au jalon 0 : cadrage du Domain, scénarios de référence et décisions fondatrices. Le moteur métier n'est pas encore implémenté.

## Prérequis

- .NET SDK 10.0.102 ou patch compatible ;
- Docker avec Docker Compose, à partir du jalon 2.

## Commandes

```bash
dotnet restore --locked-mode
dotnet build --no-restore -warnaserror
dotnet test --no-build
```

Lors du premier restore, exécuter `dotnet restore` afin de générer les lock files, puis les versionner.

## Architecture

- `GenEngine.Narrative` : moteur narratif pur ;
- `GenEngine.Authoring` : brouillons, validation et publication ;
- `GenEngine.Play` : sessions et orchestration du runtime ;
- `GenEngine.Identity` : authentification locale ;
- `GenEngine.Infrastructure` : persistance et adaptateurs ;
- `GenEngine.Api` : surface HTTP et composition.

Les décisions, invariants et tâches sont documentés dans [`specs/`](specs/README.md).
