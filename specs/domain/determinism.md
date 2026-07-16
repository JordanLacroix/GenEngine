# Déterminisme et compatibilité

Le résultat dépend uniquement du snapshot publié, de l'état initial, de la graine et de la suite ordonnée des commandes. Les accès réseau, disque, horloge et base sont exclus du moteur pur.

Le hash d'une publication est le SHA-256 de sa représentation JSON canonique. Le PRNG contractuel est SplitMix64 et possède des vecteurs de test. La politique détaillée de compatibilité et de replay est définie par l'[ADR 0003](../adr/0003-determinism-versioning-replay.md).
