# Rotation quotidienne

## Intention

Trois scénarios sont mis en avant chaque jour. L'objectif est de donner un point d'entrée court à un joueur qui ouvre l'application sans savoir quoi faire, sans transformer le catalogue en file de tâches.

## Règle

Déterministe, sans état stocké, sans horloge système côté moteur :

```text
selection(jour) = [ pool[((jour * taille) + offset) mod |pool|] pour offset dans [0, taille) ]
```

avec `taille = 3` et `pool` = les dix slugs de Diapason dans l'ordre du manifeste.

Propriétés, vérifiées par `DiapasonContentTests.ManifestRotationIsDeterministicAndCoversEveryEligibleScenario` :

- **même jour → même sélection**, pour toujours et sur toutes les instances ;
- `pgcd(3, 10) = 1`, donc dix jours consécutifs couvrent exactement les dix scénarios, sans trou ni répétition dans le cycle ;
- aucune graine aléatoire, donc aucune divergence entre le client Web, le client iOS et le serveur.

`jour` est un entier. La source retenue est le **jour logique** de la plateforme, jamais `DateTime.Now` côté moteur : l'invariant 10 interdit au moteur narratif de consulter l'horloge système. La conversion date calendaire → index de jour est une responsabilité de la couche de présentation, qui la calcule une fois à partir du fuseau déclaré dans `GameDefinition.TimeZone`.

## État d'implémentation

**Documentée et testée, non exposée par une API.**

Le modèle de configuration actuel (`ExperienceDocument`, `CategoryDefinition`, `JourneyDefinition`, `CatalogAssignmentDefinition`) ne comporte aucun champ de mise en avant, de fenêtre glissante ou de rotation. `CatalogAssignmentDefinition` porte bien `AvailableFrom` et `DueAt`, mais ce sont des bornes d'affectation par unité d'organisation, pas un mécanisme de rotation : les utiliser reviendrait à réécrire trois cent soixante-cinq affectations par an et par unité.

La consigne étant de ne rien inventer qui ne rentre pas dans le modèle existant, la rotation est livrée comme **règle publiée dans le manifeste** (`dailyRotation`), avec une implémentation de référence dans les tests. Aucun endpoint n'a été ajouté. Un client peut appliquer la règle localement dès aujourd'hui, sans appel serveur, puisqu'il connaît le manifeste et l'index du jour.

Pour l'exposer côté serveur, il faudrait ajouter au `ExperienceDocument` une définition de type `SpotlightDefinition(bool Enabled, int Size, IReadOnlyList<Guid> Pool)`, ses défauts, ses portées, sa validation et ses permissions — ce qui relève d'une décision produit et d'une mise à jour de `specs/configuration-catalog.md`, pas d'un lot de contenu.

## Prérequis au niveau scénario : nécessaires ?

**Non, en l'état.**

Diapason exprime toute sa progression par les trois parcours et leurs `PrerequisiteJourneyIds`, et cela suffit : les dépendances réelles sont entre postures (on n'arbitre pas sans avoir établi les faits), pas entre scénarios individuels. À l'intérieur d'un parcours, l'ordre proposé par `order` est une recommandation de lecture, et rien ne justifie de bloquer « Le tri des candidatures » derrière « La revue automatique » — les deux exercent l'arbitrage sur des matières différentes.

Un prérequis au niveau scénario deviendrait nécessaire dans deux cas, qui n'existent pas dans ce lot : une suite narrative où un scénario reprend l'état d'un autre, ou un scénario de remédiation débloqué par un échec précis. Le second suppose de toute façon un drapeau d'échec de première classe, qui manque également (voir `scenarios.md`).

Recommandation : ne pas ajouter de prérequis au niveau scénario tant qu'un de ces deux besoins n'est pas validé par un usage réel.
