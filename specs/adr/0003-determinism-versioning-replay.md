# ADR 0003 — Déterminisme, snapshots et replay

## Statut

Accepté.

## Contexte

Une session doit rester rejouable sans dépendre d'un brouillon modifié, de la culture de la machine ou d'un générateur aléatoire implicite.

## Décision

- Une publication crée un snapshot JSON immuable et son SHA-256 canonique.
- La canonicalisation trie récursivement les propriétés d'objet, conserve l'ordre des tableaux et écrit un JSON UTF-8 compact.
- Une session référence exclusivement l'identifiant, le contenu et le hash de sa version publiée.
- Le PRNG est SplitMix64 ; sa graine et son état font partie de l'état persistant.
- Une commande possède un identifiant stable. Sa première réponse est persistée et rejouée à l'identique en cas de doublon.
- Le replay est garanti à moteur majeur, snapshot, état initial, graine et suite de commandes identiques. Un changement incompatible exige une nouvelle version majeure du runtime.

## Conséquences

Les nouvelles publications peuvent corriger un scénario sans modifier les sessions existantes. Le stockage est plus volumineux, mais l'audit et la reproductibilité ne dépendent pas de l'état courant d'Authoring.

## Alternatives écartées

- Relire le brouillon courant : détruit l'immuabilité.
- Utiliser `System.Random` sans contrat versionné : séquence non contractuelle.
- Rejouer uniquement depuis les tables Authoring : couple la disponibilité et le schéma des services.
