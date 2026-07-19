# Licences du pack d'assets Diapason

Ce fichier fait autorité sur la provenance et la licence de **chaque fichier** livré
sous `assets/diapason/`. Il ne concerne que ces assets : le code source de GenEngine
n'a pas encore de licence (voir la section « Licence » du [README](../../README.md)).

## Résumé

| Élément | Valeur |
|---|---|
| Nombre d'assets livrés | 62 |
| Sources distinctes | 4 packs Kenney |
| Licence de tous les assets | **CC0 1.0 Universal** (domaine public) |
| Auteur de tous les assets | **Kenney** (Kenney Vleugels), <https://kenney.nl> |
| Assets sous une autre licence | aucun |
| Assets dont la licence n'a pas été vérifiée | aucun |

Tous les fichiers ont été téléchargés depuis les URL officielles `kenney.nl`, et le
fichier de licence fourni par Kenney à l'intérieur de chaque archive est conservé
tel quel sous [`licenses/`](licenses/). Aucun fichier n'a été modifié : les octets
livrés sont identiques à ceux de l'archive amont, ce que le manifeste permet de
revérifier via l'empreinte SHA-256 de chaque fichier.

## Attribution

CC0 n'impose aucune attribution, et Kenney précise explicitement que le crédit
« serait apprécié mais n'est pas obligatoire ». Le projet choisit néanmoins de
créditer systématiquement.

**Texte d'attribution à afficher** (crédits de l'application, page « à propos »,
documentation) :

> Éléments d'interface et sons : **Kenney** — <https://kenney.nl> — CC0 1.0.

## Détail par pack

### 1. UI Pack (2.0)

| Champ | Valeur |
|---|---|
| Identifiant manifeste | `kenney-ui-pack` |
| Page officielle | <https://kenney.nl/assets/ui-pack> |
| Archive téléchargée | <https://kenney.nl/media/pages/assets/ui-pack/f651646eab-1718203990/kenney_ui-pack.zip> |
| SHA-256 de l'archive | `a8a14a234911eb648c062622915c93e79e94e97cb7f9f375a70f6617f1174318` |
| Auteur | Kenney (Kenney Vleugels) |
| Licence | CC0 1.0 — <https://creativecommons.org/publicdomain/zero/1.0/> |
| Licence amont conservée | [`licenses/kenney_ui-pack.License.txt`](licenses/kenney_ui-pack.License.txt) |
| Attribution demandée | « Support us by crediting Kenney or `www.kenney.nl` (this is not mandatory) » |
| Fichiers repris | 26 SVG (`ui/`), issus de `Vector/Grey` et `Vector/Extra` |

### 2. Game Icons

| Champ | Valeur |
|---|---|
| Identifiant manifeste | `kenney-game-icons` |
| Page officielle | <https://kenney.nl/assets/game-icons> |
| Archive téléchargée | <https://kenney.nl/media/pages/assets/game-icons/1ebf9c14af-1677661579/kenney_game-icons.zip> |
| SHA-256 de l'archive | `7a86d8d58e0b851e22004b3c70bf90b003632bbf9ac633424daa3bb17d9e7e4e` |
| Auteur | Kenney (Kenney Vleugels) |
| Licence | CC0 1.0 — <https://creativecommons.org/publicdomain/zero/1.0/> |
| Licence amont conservée | [`licenses/kenney_game-icons.license.txt`](licenses/kenney_game-icons.license.txt) |
| Attribution demandée | « Credit (Kenney or `www.kenney.nl`) would be nice but is not mandatory. » |
| Fichiers repris | 20 PNG (`icons/`), issus de `PNG/White/2x` |

### 3. Interface Sounds (1.0)

| Champ | Valeur |
|---|---|
| Identifiant manifeste | `kenney-interface-sounds` |
| Page officielle | <https://kenney.nl/assets/interface-sounds> |
| Archive téléchargée | <https://kenney.nl/media/pages/assets/interface-sounds/fa43c1dd4d-1677589452/kenney_interface-sounds.zip> |
| SHA-256 de l'archive | `f2193d072726d6758a5f7871b2dcc54dcce0d5c35c6f0a62f92549b327c81232` |
| Auteur | Kenney (Kenney Vleugels) |
| Licence | CC0 1.0 — <https://creativecommons.org/publicdomain/zero/1.0/> |
| Licence amont conservée | [`licenses/kenney_interface-sounds.License.txt`](licenses/kenney_interface-sounds.License.txt) |
| Attribution demandée | « Support us by crediting Kenney or `www.kenney.nl` (this is not mandatory) » |
| Fichiers repris | 12 OGG (`sfx/`), issus de `Audio/` |

### 4. Music Jingles

| Champ | Valeur |
|---|---|
| Identifiant manifeste | `kenney-music-jingles` |
| Page officielle | <https://kenney.nl/assets/music-jingles> |
| Archive téléchargée | <https://kenney.nl/media/pages/assets/music-jingles/f37e530b9e-1677590399/kenney_music-jingles.zip> |
| SHA-256 de l'archive | `b729ba57959bd58793d2c5cafa348aaf2655d354f3da35ec4729e03ec77197b8` |
| Auteur | Kenney (Kenney Vleugels) |
| Licence | CC0 1.0 — <https://creativecommons.org/publicdomain/zero/1.0/> |
| Licence amont conservée | [`licenses/kenney_music-jingles.License.txt`](licenses/kenney_music-jingles.License.txt) |
| Attribution demandée | « Credit (Kenney or `www.kenney.nl`) would be nice but is not mandatory. » |
| Fichiers repris | 4 OGG (`stingers/`), issus de `Audio/Steel jingles` |

## Ce qui n'a délibérément **pas** été inclus

- **Aucune ambiance ni musique longue.** Le catalogue audio de Kenney ne contient que
  des sons courts et des jingles ; aucune boucle d'ambiance n'y existe. Plutôt que de
  substituer une source dont la licence serait moins nette, le pack ne livre rien
  sur cette catégorie et le manque est déclaré dans le manifeste (`gaps`).
- **Aucune illustration.** Kenney ne publie pas d'illustration 2D peinte compatible
  avec la direction artistique Diapason.
- **Aucune police.** Les polices présentes dans l'archive `UI Pack` n'ont pas été
  reprises : le pack n'en a pas besoin et une police engage des contraintes
  d'intégration distinctes.

## Règles pour ajouter un asset

Un asset ne peut être ajouté à ce pack que si les cinq conditions suivantes sont
réunies. En cas de doute sur l'une d'elles, **l'asset n'est pas ajouté**.

1. **Licence permissive et vérifiée à la source.** CC0, ou une licence explicitement
   compatible avec un usage commercial et une redistribution. La licence doit être
   lue sur la page officielle de l'auteur ou dans le fichier de licence livré avec
   l'archive, pas déduite d'un agrégateur ou d'un miroir.
2. **Provenance officielle.** Le fichier est téléchargé depuis le domaine de l'auteur
   ou de l'éditeur d'origine. Les miroirs et redistributions tierces sont refusés.
3. **Traçabilité complète.** L'ajout renseigne dans ce fichier et dans le manifeste :
   URL de la page, URL de l'archive, SHA-256 de l'archive, auteur, licence, URL de la
   licence, texte d'attribution, et copie du fichier de licence amont sous
   `licenses/`.
4. **Attribution effective.** Même lorsque la licence ne l'exige pas, l'auteur est
   crédité dans ce fichier et dans le texte d'attribution affiché par les clients.
5. **Vérification mécanique.** `python3 scripts/verify-asset-manifest.py` passe sans
   erreur : le fichier existe, sa taille et son empreinte correspondent, et il se
   décode réellement comme le type qu'il déclare.

Les licences interdisant l'usage commercial (`NC`), interdisant les œuvres dérivées
(`ND`), imposant un partage à l'identique sur l'ensemble du projet, ou exigeant une
licence d'éditeur nominative sont refusées pour ce pack.
