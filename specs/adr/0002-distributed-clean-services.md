# ADR 0002 — Services distribués avec Clean Architecture

- Statut : accepté
- Date : 2026-07-16
- Remplace : [ADR 0001](0001-pragmatic-ddd-clean-modular-monolith.md)

## Contexte

La contrainte d’architecture exclut un monolithe, y compris modulaire. Les domaines d’authoring, de jeu et d’identité doivent pouvoir évoluer, être livrés et être opérés indépendamment.

Le runtime narratif doit cependant rester strictement déterministe. Le transformer en service distant placerait la latence et la disponibilité réseau dans chaque transition de jeu.

## Décision

GenEngine est organisé en trois services autonomes : `Authoring`, `Play` et `Identity`.

Chaque service :

- possède un exécutable API, une base, ses migrations et sa configuration ;
- applique Clean Architecture avec des projets `Domain`, `Application`, `Infrastructure` et `Api` ;
- ne référence aucun projet d’un autre service ;
- échange via des contrats réseau versionnés ou des événements d’intégration.

Le moteur `Narrative` reste une bibliothèque métier pure, versionnée et embarquée par `Authoring.Application` et `Play.Application`.

## Conséquences

### Positives

- Déploiements et propriété des données indépendants.
- Frontières DDD garanties par le compilateur et la CI.
- Pas de base, transaction ou infrastructure partagée entre services.
- Runtime narratif sans dépendance réseau et reproductible.
- Possibilité de dimensionner `Play` séparément des outils d’authoring.

### Négatives

- Davantage de projets, images, configurations et pipelines.
- Cohérence interservices nécessairement explicite et souvent éventuelle.
- Besoin de versionner les contrats et d’observer les appels distribués.
- Risque de divergence de version du package Narrative, traité par tests de compatibilité et montée explicite.

## Alternatives considérées

### Monolithe modulaire

Rejeté : il conserve un seul cycle de déploiement et ne respecte pas la contrainte produit.

### Moteur Narrative exposé comme microservice

Rejeté : il ajouterait une panne réseau et de la latence au chemin déterministe critique.

### Package partagé contenant tous les modèles métier

Rejeté : seul le moteur pur Narrative est partagé. Les domaines Authoring, Play et Identity restent privés à leurs services.

### Base PostgreSQL partagée

Rejetée : elle permettrait des jointures et transactions transversales qui annuleraient l’autonomie des services.
