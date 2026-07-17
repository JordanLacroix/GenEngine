# Invariants

1. Une version publiée est immuable.
2. Une session reste attachée à sa version publiée initiale.
3. Le moteur n'effectue aucun accès réseau, disque ou base de données.
4. À versions, état initial, graine et commandes identiques, la suite d'états est identique.
5. Une commande joueur possède un identifiant d'idempotence.
6. Une commande acceptée ne peut produire ses effets qu'une seule fois.
7. Le reducer ne modifie que le `WorldState` narratif local.
8. Aucun script ou expression arbitraire fourni par un auteur n'est exécuté.
9. Les anciennes sessions ne sont jamais rebasées silencieusement sur un nouveau snapshot.
10. Le temps narratif est une donnée logique explicite de la session ; le moteur ne consulte jamais l'horloge système.
