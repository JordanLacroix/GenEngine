# Glossaire

- **Brouillon** : document narratif modifiable, non jouable en production.
- **Snapshot** : représentation runtime compilée, canonique et immuable d'une version publiée.
- **Session** : instance de jeu attachée à un snapshot précis.
- **WorldState** : état narratif local immuable d'une session.
- **Commande** : entrée joueur ordonnée et idempotente.
- **Runtime** : fonction pure qui calcule le prochain état à partir du snapshot, de l'état et d'une commande.
- **Replay** : réexécution d'une session à partir de ses versions, de sa graine et de ses commandes acceptées.
