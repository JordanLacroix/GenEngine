# ADR 0001 — Monolithe modulaire DDD/Clean pragmatique

- Statut : remplacé par [ADR 0002](0002-distributed-clean-services.md)
- Date : 2026-07-16

## Contexte

> Cette décision a été remplacée dès le cadrage : la contrainte produit exige des services indépendamment déployables et exclut un monolithe.

Le squelette initial séparait les bounded contexts, un `SharedKernel` et une infrastructure globale. Bien qu’aucune logique métier ne soit encore implémentée, cette direction rendait probable un couplage transversal : l’infrastructure connaissait tous les modules et le Shared Kernel pouvait devenir un fourre-tout.

Une Clean Architecture constituée de quatre projets par module donnerait des frontières de compilation fortes, mais créerait immédiatement de nombreux assemblies vides et une charge disproportionnée avant l’implémentation des domaines correspondants.

## Décision

GenEngine adopte un monolithe modulaire avec :

- un assembly par bounded context ;
- une organisation Clean Architecture interne et par tranches verticales ;
- une infrastructure possédée par chaque module ;
- un moteur `Narrative` pur, indépendant des frameworks ;
- une API limitée au rôle d’hôte et de composition root ;
- une liste blanche exhaustive des références entre projets, vérifiée automatiquement ;
- aucun Shared Kernel tant qu’un modèle partagé réel n’est pas démontré.

## Conséquences

### Positives

- Les responsabilités et la propriété des données sont locales à chaque bounded context.
- Le cœur narratif reste testable sans HTTP, base de données ou conteneur DI.
- Les dépendances accidentelles entre modules font échouer la CI.
- Le nombre de projets reste proportionné à l’état du produit.
- Les modules pourront être extraits ou éclatés à partir de signaux concrets.

### Négatives

- Les couches internes d’un module ne sont pas encore isolées par le compilateur.
- La discipline `internal` et les tests de namespaces devront compléter les références de projets lorsque le code apparaîtra.
- Les échanges asynchrones entre modules demanderont une infrastructure d’intégration explicite au jalon concerné.

## Alternatives considérées

### Infrastructure globale et Shared Kernel précréé

Rejetée : elle centralise les adaptateurs de tous les modules et encourage le partage sans preuve métier.

### Un projet par couche et par module

Différée : excellente isolation, mais trop de structure vide au stade de ce cadrage. Elle reste une trajectoire possible module par module.

### Microservices dès le départ

Rejetée : les coûts opérationnels, transactions distribuées et contrats réseau ne répondent à aucun besoin actuel.

### Monolithe sans frontières de projets

Rejetée : les bounded contexts seraient seulement documentés et trop faciles à contourner.
