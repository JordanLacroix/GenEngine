# Politique de sécurité

## Versions prises en charge

GenEngine est en phase préliminaire. Seule la branche `main` reçoit des correctifs de sécurité jusqu’à la publication d’une première version stable.

| Version | Prise en charge |
|---|---|
| `main` | Oui |
| Anciennes révisions | Non |

## Signaler une vulnérabilité

N’ouvrez pas d’issue publique et ne publiez pas de preuve d’exploitation.

Utilisez le [signalement privé GitHub](https://github.com/JordanLacroix/GenEngine/security/advisories/new) en fournissant :

- le composant et la révision concernés ;
- l’impact et les préconditions ;
- des étapes de reproduction minimales ;
- une preuve nettoyée de toute donnée sensible ;
- une proposition de correction, si disponible.

Un accusé de réception est visé sous 7 jours. Après validation, la correction est préparée de façon privée et la divulgation est coordonnée. Aucun programme de prime n’est actuellement proposé.

## Périmètre

Sont notamment concernés : exécution de code, contournement d’autorisation, exposition de secrets ou données, altération non autorisée de l’état narratif et vulnérabilités de la chaîne de dépendances.

Les questions de durcissement sans scénario exploitable peuvent être proposées dans une issue publique, sans y inclure d’information sensible.
