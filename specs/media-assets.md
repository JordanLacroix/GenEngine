# Packs d'assets et manifeste média

Ce document décrit le format de pack d'assets de GenEngine et son premier
exemplaire, `diapason-core`, livré sous [`assets/diapason/`](../assets/diapason/).

Un pack d'assets est **de la donnée, pas du code**. Il ne contient aucune logique,
n'est pas compilé et ne traverse aucune frontière de service : il est publié comme un
contenu statique et référencé par identifiant depuis une configuration.

## 1. Ce que contient le pack `diapason-core`

62 fichiers, 640 Kio au total, tous sous licence **CC0 1.0** et tous produits par
**Kenney** (<https://kenney.nl>). Le détail par fichier, avec provenance et
empreinte, se trouve dans [`assets/diapason/LICENSES.md`](../assets/diapason/LICENSES.md).

| Dossier | Contenu | Format | Nombre |
|---|---|---|---:|
| `ui/` | Cadres de panneau 9-slice, boutons, champs de saisie, séparateurs, cases à cocher, curseurs, glyphes et flèches | SVG | 26 |
| `icons/` | Icônes de HUD : paramètres, aide, avertissement, récompense, verrouillage, son, musique, navigation | PNG 100×100 RGBA | 20 |
| `sfx/` | Sons d'interface : survol, confirmation, clic, erreur, retour, ouverture/fermeture de panneau, carte, accent cristallin, bascule, progression | OGG Vorbis mono | 12 |
| `stingers/` | Signatures courtes métalliques : récompense principale et secondaire, ouverture et clôture de session | OGG Vorbis | 4 |

### Cohérence visuelle

Les visuels amont sont livrés en **gris neutre**. C'est un choix délibéré : la palette
Diapason (`encre #17344a`, `ivoire #fffaf0`, `sauge #7a9a55`, `or #d7a746`,
`azur #2f7fa0`) est appliquée **côté client** — `currentColor` et `fill` pour les SVG,
filtre de teinte ou masque pour les PNG. Les fichiers sources ne sont jamais modifiés,
ce qui garde les octets identiques à l'archive amont et rend la provenance
revérifiable par empreinte.

Les quatre packs proviennent du même auteur et partagent la même grammaire plate et
sobre : aucun mélange de styles.

### Ce que le pack ne contient pas

Le manifeste déclare explicitement ses manques dans le tableau `gaps`.

- **Ambiances** : absentes. Le catalogue audio de Kenney ne propose que des sons
  courts et des jingles ; il n'existe pas de boucle d'ambiance CC0 chez cet auteur.
  Aucune source non CC0 n'a été substituée.
- **Illustrations** : absentes. Kenney ne publie pas d'illustration 2D peinte
  compatible avec la direction artistique Diapason.
- **Musique** : partielle. Seules des signatures courtes sont disponibles ; les pistes
  instrumentales par acte restent à produire.

Ces trois manques sont des décisions assumées, pas des oublis. Ils sont dans le
manifeste pour qu'un client puisse détecter l'absence et dégrader proprement plutôt
que d'échouer sur une ressource introuvable.

## 2. Format du manifeste

Le manifeste est [`assets/diapason/asset-manifest.json`](../assets/diapason/asset-manifest.json).
Il est autosuffisant : tout ce dont un client a besoin pour charger et afficher un
asset s'y trouve, sans lecture du système de fichiers ni requête supplémentaire.

### En-tête

| Champ | Type | Rôle |
|---|---|---|
| `schemaVersion` | entier | Version du schéma de manifeste. Un changement incompatible l'incrémente. |
| `packId` | chaîne | Identifiant stable du pack (`diapason-core`). |
| `packVersion` | chaîne | Version sémantique du contenu du pack. |
| `configurationKey` | chaîne | Configuration à laquelle le pack est destiné. |
| `basePath` | chaîne | Racine des chemins relatifs, depuis la racine du dépôt. |
| `palette` | objet | Palette nommée à appliquer à la recoloration. |
| `recoloring` | chaîne | Règle de teinte : appliquée côté client, jamais dans les fichiers. |
| `gaps` | tableau | Catégories volontairement absentes, avec justification. |
| `sources` | tableau | Provenance et licence, une entrée par pack amont. |
| `assets` | tableau | Un objet par fichier livré. |

### Entrée de `sources`

```json
{
  "sourceId": "kenney-ui-pack",
  "name": "UI Pack (2.0)",
  "author": "Kenney (Kenney Vleugels)",
  "pageUrl": "https://kenney.nl/assets/ui-pack",
  "downloadUrl": "https://kenney.nl/media/pages/assets/ui-pack/.../kenney_ui-pack.zip",
  "archiveSha256": "a8a14a2349...",
  "license": "CC0-1.0",
  "licenseUrl": "https://creativecommons.org/publicdomain/zero/1.0/",
  "licenseFile": "licenses/kenney_ui-pack.License.txt",
  "attribution": "UI Pack (2.0) par Kenney — www.kenney.nl — CC0 1.0"
}
```

`license` utilise un identifiant SPDX. `attribution` est le texte prêt à afficher.
`downloadUrl` et `archiveSha256` documentent la provenance ; **ils ne sont jamais
appelés à l'exécution** : les assets livrés sont servis depuis le dépôt, aucun
client ne doit pointer vers un serveur tiers.

### Entrée de `assets`

```json
{
  "id": "ui.button.primary",
  "kind": "ui",
  "role": "button",
  "path": "ui/button_rectangle_flat.svg",
  "bytes": 854,
  "sha256": "48bf9cae58d01b3ee3edd4d43cf7fefc19f82e319c05c17e3da77ee8ffbd06f3",
  "sourceId": "kenney-ui-pack",
  "usage": "Bouton de choix principal, état par défaut",
  "mediaType": "image/svg+xml",
  "image": {
    "width": 192,
    "height": 64,
    "scalable": true,
    "transparency": true,
    "recolorable": true,
    "nineSliceInsets": { "left": 8, "right": 8, "top": 8, "bottom": 8 }
  }
}
```

| Champ | Présence | Rôle |
|---|---|---|
| `id` | toujours | **Identifiant stable**, unique dans le pack. C'est la seule clé qu'une configuration référence. |
| `kind` | toujours | `ui`, `icon`, `sfx` ou `stinger`. |
| `role` | toujours | Sous-catégorie : `panel`, `button`, `input`, `divider`, `control`, `glyph`, `one-shot`. |
| `path` | toujours | Chemin relatif à `basePath`, toujours relatif, jamais `..`. |
| `bytes`, `sha256` | toujours | Intégrité et détection de dérive. |
| `sourceId` | toujours | Renvoie vers `sources` pour la licence et l'attribution. |
| `usage` | toujours | Usage recommandé, en français, à destination des auteurs. |
| `mediaType` | toujours | Un des types servis : `image/svg+xml`, `image/png`, `audio/ogg`, `audio/mp4`, `audio/aac`, `audio/mpeg`. Voir « Formats audio servis ». |
| `image` | visuels | `width`, `height`, `scalable`, `transparency`, `recolorable`, et `nineSliceInsets` quand l'asset est étirable. |
| `audio` | sons | `codec`, `sampleRate`, `channels`, `durationSeconds`, `loop`, `loopPoints`. |
| `review` | optionnel | Réserve explicite sur l'asset, à lever par une passe humaine. |

`loop` et `loopPoints` sont présents et valués à `false` / `null` sur tous les sons
actuels : aucun n'est bouclable. Les champs existent pour que l'ajout futur d'une
ambiance ne change pas la forme du schéma.

### Contrat pour l'intégration moteur

Le manifeste est conçu pour être consommé sans transformation :

- une seule liste plate `assets`, pas d'arborescence à parcourir ;
- `id` est la clé primaire, stable dans le temps, indépendante du chemin ;
- `path` peut changer sans casser une configuration, tant que `id` est conservé ;
- `kind` suffit à router vers le bon lecteur (image ou audio) ;
- toutes les métadonnées nécessaires au rendu (dimensions, durée, canaux, 9-slice)
  sont présentes, aucune inspection de fichier n'est requise au démarrage ;
- `gaps` permet de savoir ce qui est absent sans le déduire d'un échec.

Renommer un `id` est un **changement incompatible** : il faut incrémenter
`packVersion` en majeure et documenter la correspondance.

## 3. Référencer un asset depuis une configuration

Une configuration ne cite jamais un chemin de fichier. Elle cite un identifiant
qualifié par le pack, sous la forme `{packId}:{assetId}` :

```text
diapason-core:ui.panel.frame
diapason-core:sfx.choice.confirm
diapason-core:icon.reward-trophy
```

Cette forme est stable, vérifiable hors ligne et lisible dans un diff.

Usages attendus, cohérents avec le catalogue de configuration (`media.*`) :

| Point de référence | Exemple |
|---|---|
| Étape de scénario | son d'ouverture de panneau, illustration de fond |
| Choix | son de survol et son de confirmation |
| Récompense | icône et signature sonore |
| Emplacement d'application | cadre de panneau, icône de barre de HUD |
| Terminologie et thème de front | palette et jeu d'icônes du pack actif |

Règles :

1. **Une référence inconnue n'interrompt jamais le parcours.** Un asset absent ou un
   pack non chargé conduit à un rendu dégradé, jamais à une erreur bloquante — c'est
   la même exigence que pour l'IA (invariant 14) : le média est un enrichissement.
2. **Aucune information n'est portée exclusivement par un asset.** Un son ou une
   icône double toujours une information déjà disponible en texte, conformément à la
   direction artistique et aux exigences d'accessibilité.
3. **Une session utilise le pack figé dans son snapshot de configuration**
   (invariant 12). Republier un pack ne modifie pas les sessions en cours.

## 3 bis. Comment le pack est publié et servi

Le pack n'est plus de la donnée inerte : `Configuration` le publie et sert ses octets.

| Route | Rôle |
|---|---|
| `GET /asset-packs` | Liste des packs livrés par l'instance. |
| `GET /asset-packs/{packId}` | Manifeste complet, `path` réécrit en chemin servable. |
| `GET /asset-packs/{packId}/files/{chemin}` | Octets de l'asset. |

**Pourquoi `Configuration` et pas un service dédié.** Un pack est un paramètre
d'expérience, pas un domaine : il décrit ce qu'une instance publie, exactement
comme la configuration décrit ce qu'elle active. L'ADR
[`0005`](adr/0005-configuration-control-plane.md) fait de `Configuration` le
control plane ; y ajouter la lecture d'un pack n'introduit ni frontière, ni base,
ni mécanisme parallèle. Aucun service n'est ajouté.

**Pourquoi ces routes sont anonymes.** Elles le sont pour la même raison que
`GET /experience/{frontId}` : la démonstration s'adresse à un visiteur anonyme, et
un asset placé derrière un jeton rendrait le parcours hors ligne dépendant d'une
session. Le contenu servi est du CC0 public ; il ne porte aucune donnée
d'instance.

**Contrat de service.** Les types servis sont déclarés explicitement plutôt que
déduits : un navigateur refuse de décoder un son servi en
`application/octet-stream`. Les réponses portent
`Cache-Control: public, max-age=31536000, immutable` — les octets d'un `packVersion`
donné ne changent jamais — et `X-Content-Type-Options: nosniff`. Un chemin
remontant (`..`) ou absolu déclaré dans un manifeste fait **échouer le démarrage**
au lieu d'être monté.

### Formats audio servis

| Extension | `mediaType` | Décodé par Safari / `AVAudioPlayer` |
|---|---|:--:|
| `.svg` | `image/svg+xml` | — |
| `.png` | `image/png` | — |
| `.ogg` | `audio/ogg` | **non** |
| `.m4a` | `audio/mp4` | oui |
| `.aac` | `audio/aac` | oui |
| `.mp3` | `audio/mpeg` | oui |

L'OGG Vorbis n'est décodé **ni par Safari, ni par `AVAudioPlayer` sur iOS**. Un
pack qui ne livrerait que de l'OGG serait donc structurellement muet sur tout
l'écosystème Apple : ce n'est pas une préférence de format, c'est un défaut
produit. L'AAC — en conteneur MP4 (`.m4a`) ou en flux brut (`.aac`) — et le MP3
sont décodés partout ; ils sont servables même si aucun pack livré aujourd'hui
n'en contient. Un pack de surcharge ou une future ambiance doit préférer
l'AAC : c'est le seul format à couvrir la totalité des clients visés.

La liste reste une **liste blanche** : toute extension absente du tableau renvoie
404 au lieu d'être servie avec un type deviné ou reniflé.

**Lecture seule.** Les packs sont copiés dans l'image (`COPY assets/`), possédés
par l'utilisateur non-root, et lus une seule fois au démarrage. Rien n'est écrit à
l'exécution : le système de fichiers du conteneur reste en lecture seule.

**Le client web sert aussi le pack.** `GenEngine.Web` embarque les mêmes octets
sous `public/packs/` et publie son propre `/packs/manifest.json`. C'est délibéré :
la démonstration doit tourner **sans backend**, et la seule origine qu'un visiteur
anonyme atteint alors est celle qui sert l'application. Les deux copies sont
identiques par empreinte SHA-256, vérifiée de chaque côté par un test.

## 4. Surcharger ou étendre le pack dans une instance client

Une organisation cliente doit pouvoir apposer sa propre identité sans forker le
dépôt. Trois mécanismes, du plus léger au plus lourd :

### a. Recoloration

Le mécanisme par défaut, et celui à préférer. Le pack est neutre ; la configuration
de front fournit sa propre palette et les mêmes fichiers rendent une identité
différente. Aucun asset n'est dupliqué.

### b. Surcharge par identifiant

L'instance déclare un pack de surcharge qui réutilise **les mêmes `id`** :

```json
{
  "schemaVersion": 1,
  "packId": "acme-brand",
  "packVersion": "1.0.0",
  "extends": "diapason-core",
  "assets": [
    { "id": "icon.reward-trophy", "kind": "icon", "path": "icons/acme-trophy.png", "...": "..." }
  ]
}
```

Résolution : pour un `id` donné, le pack de surcharge gagne ; à défaut, le pack
`extends` répond. Un pack de surcharge n'a pas besoin d'être exhaustif — il ne
déclare que ce qu'il remplace. L'ordre de résolution est déterministe et suit la
chaîne `extends`, sans cycle.

### c. Extension par nouveaux identifiants

Un pack de surcharge peut aussi introduire des `id` inexistants en amont, pour des
contenus propres à l'organisation. Ces identifiants doivent être préfixés par un
espace de noms distinct (`acme.*`) afin qu'une future addition à `diapason-core`
n'entre jamais en collision.

Dans tous les cas :

- le pack de surcharge est soumis **aux mêmes règles de licence** que le pack de base
  et fournit son propre `LICENSES.md` et ses fichiers `licenses/` ;
- il passe la même vérification mécanique ;
- il est servi depuis l'infrastructure de l'instance, jamais depuis un serveur tiers.

## 5. Règles de licence pour ajouter un asset

Elles sont normatives et détaillées dans
[`assets/diapason/LICENSES.md`](../assets/diapason/LICENSES.md). En résumé, les cinq
conditions cumulatives : licence permissive **vérifiée à la source**, provenance
**officielle** (pas de miroir), traçabilité complète dans le manifeste et le fichier
de licences, attribution effective même quand elle n'est pas obligatoire, et
vérification mécanique réussie.

Rappel de contexte : le dépôt est public mais **le code n'a pas encore de licence**.
Le choix du CC0 pour la totalité des assets est délibéré — il garantit qu'aucun
fichier livré ici ne contraindra la licence retenue plus tard pour le code.

En cas de doute sur une licence, l'asset n'est pas ajouté.

## 6. Vérification

```bash
python3 scripts/verify-asset-manifest.py
```

Le script est sans dépendance externe. Il contrôle que le manifeste est un JSON
valide, que chaque chemin déclaré résout vers un fichier existant, que la taille et
l'empreinte SHA-256 correspondent, que **chaque fichier se décode réellement comme
le type qu'il déclare** (signature PNG et chunk `IHDR`, signature `OggS` et en-tête
d'identification Vorbis, élément racine et espace de noms SVG), que les dimensions et
durées déclarées correspondent aux en-têtes décodés, que chaque `sourceId` existe et
dispose de son fichier de licence, et qu'aucun fichier livré n'est absent du
manifeste.

La durée audio est recalculée à partir du `granule position` de la dernière page Ogg :
elle n'est pas recopiée depuis l'outil qui a généré le manifeste, ce qui rend le
contrôle indépendant de la génération.

**Ce recalcul ne vaut que pour l'OGG Vorbis.** Pour l'AAC (`audio/mp4`,
`audio/aac`) et le MP3, le script vérifie le conteneur — boîtes `ftyp` et `moov`,
synchronisation ADTS, en-tête ID3 ou synchronisation de trame MPEG — et exige que
`codec`, `channels`, `sampleRate` et `durationSeconds` soient déclarés, mais il ne
recalcule ni durée ni fréquence : les obtenir imposerait de parcourir la
hiérarchie de boîtes MP4 ou des trames à débit variable, ce qu'un script sans
dépendance ne peut pas faire honnêtement. Pour ces formats la durée est donc
**déclarative, pas prouvée** ; c'est écrit ici plutôt que masqué par un contrôle
qui ferait semblant.
