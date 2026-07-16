## Résumé

<!-- Que change cette PR, en quelques phrases ? -->

## Issue liée

Closes #

## Pourquoi

<!-- Problème résolu, valeur apportée et contexte utile. -->

## Périmètre

### Inclus

-

### Hors périmètre

-

## Implémentation

<!-- Décisions techniques importantes, compromis et éventuelles migrations. -->

## Contexte pour la revue humaine ou IA

- **Invariants à préserver :**
- **Fichiers/chemins critiques :**
- **Risques connus :**
- **Points à challenger :**

## Validation

<!-- Commandes exécutées et résultats observés. -->

```text
dotnet restore --locked-mode
dotnet build --no-restore -warnaserror
dotnet test --no-build
```

## Déploiement et retour arrière

- **Impact déploiement :** aucun / préciser
- **Migration de données :** aucune / préciser
- **Plan de rollback :** revert / préciser

## Checklist

- [ ] La PR est focalisée et son issue est liée.
- [ ] Le code compile sans warning et les tests pertinents passent.
- [ ] Les nouveaux comportements sont couverts par des tests.
- [ ] Les invariants, frontières de modules et règles de sécurité sont préservés.
- [ ] Les changements d’API, de schéma ou de configuration sont documentés.
- [ ] Le README, les specs, tâches et ADR concernés sont à jour.
- [ ] Aucune clé, donnée personnelle ou information sensible n’est présente.
- [ ] Les dépendances ajoutées sont nécessaires, maintenues et sous licence compatible.
- [ ] Les changements incompatibles et leur migration sont signalés.
- [ ] Les commentaires temporaires, logs de debug et artefacts générés sont supprimés.
