# Runtime narratif

Une session commence sur `startNodeId` avec une graine explicite. Le runtime évalue les conditions à partir du seul `WorldState`, expose les choix disponibles, applique les effets du choix retenu puis avance vers la cible.

États : `AwaitingInput`, `Paused` et `Completed`. Seule une session en attente accepte un choix. Pause et reprise sont des transitions explicites. Chaque mutation incrémente la révision de session ; une révision attendue différente provoque un conflit.

Une commande déjà traitée ne réapplique jamais ses effets : la réponse persistée est renvoyée avec l'indicateur `replayed`.
