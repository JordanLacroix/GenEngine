# Roadmap fonctionnelle du moteur

Cette roadmap rapproche progressivement le noyau actuel de la cible produit d'origine. Elle privilégie les comportements jouables et authorables ; l'infrastructure, l'IA, l'économie et les interfaces ne sont introduites que lorsqu'un cas d'usage moteur les exige.

## F1 — État joueur riche et explicable

- [x] preuves découvertes, relations, récompenses et inventaire mutable ;
- [x] historique ordonné des choix et journal d'événements notables ;
- [x] conditions et effets déclaratifs associés ;
- [x] explication arborescente de la disponibilité de chaque choix ;
- [x] simulation bornée de toutes les branches atteignables ;
- [x] validation des budgets de complexité et des ambiguïtés d'auteur.

## F2 — Interactions typées et machine à états

- [ ] séquence d'interactions par nœud : narration, choix, quiz et gate de caractéristique ;
- [ ] statuts `AwaitingValidation` et `AwaitingExternalInput` ;
- [ ] commande d'entrée typée et confirmation avant progression ;
- [ ] points d'extension pour texte libre, document, photo et dialogue IA avec fallback déterministe.

## F3 — Sauvegardes et évolution des formats

- [ ] `GameSave` explicite, historique et graine inclus ;
- [ ] migrations chaînées de scénario et de sauvegarde ;
- [ ] compatibilité de lecture des snapshots publiés v1 ;
- [ ] tests golden de migration et de replay.

## F4 — Exploration et authoring avancé

- [ ] arbre complet/parcouru/verrouillé d'une session ;
- [ ] analyse des boucles, impasses conditionnelles et fins inatteignables ;
- [ ] aperçu depuis n'importe quel nœud avec état injecté ;
- [ ] diff et restauration fonctionnelle de versions.

## F5 — Capacités transverses branchées au moteur

- [ ] caractéristiques joueur extensibles et progression de compétences ;
- [ ] événements différés par tour, condition et date logique ;
- [ ] projections journal/collection/synthèse ;
- [ ] ports déterministes pour météo, présence, temps et analyse d'entrée ;
- [ ] effets externes émis sous forme d'événements, sans couplage du moteur.

## Après le noyau

Catalogue avancé, économie, groupes, affectations, pédagogie, packs, IA optionnelle et clients seront développés à partir de ces contrats stabilisés. Ils ne doivent jamais réimplémenter les règles narratives.
