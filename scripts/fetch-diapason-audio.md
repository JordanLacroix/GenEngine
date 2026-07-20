# Kit de récupération audio CC0 — pack Diapason

Ce document est une **checklist exécutable** que l'auteur lance lui-même pour
récupérer l'audio CC0 manquant du pack `diapason-core`. Rien n'est téléchargé
automatiquement : chaque source impose soit un compte connecté (Freesound), soit
une vérification manuelle de licence à la source. La liste ci-dessous est reprise
de [`specs/domain/diapason/asset-sourcing-plan.md`](../specs/domain/diapason/asset-sourcing-plan.md)
§4, §6.1 et §6.4, où chaque licence a été vérifiée fichier par fichier.

## Règles (identiques au reste du pack)

1. Licence **CC0-1.0** uniquement, relue sur la page de la source avant tout.
2. Fichier **téléchargé et versionné** dans le dépôt ; aucun hotlink.
3. **Réencodage** en OGG Vorbis (cohérence avec le reste du pack, gain ≈ 90 %).
   Exemple : `ffmpeg -i source.wav -c:a libvorbis -q:a 4 ambience/home.ogg`
4. Après pose des fichiers : **déclarer chaque asset au manifeste**
   (`assets/diapason/asset-manifest.json`) avec `bytes`, `sha256`, `audio`
   (codec, sampleRate, channels, durationSeconds), puis **câbler** les
   `AmbienceUrl` / `MusicUrl` correspondants dans `ConfigurationService.CreateDefaultMedia`
   et le `GameOverMediaDefinition`. Tant que le fichier n'est pas posé, la
   référence **reste nulle** — ne jamais pointer un asset absent.
5. Vérification : `python3 scripts/verify-asset-manifest.py` doit rester vert.

> Freesound n'expose pas de téléchargement anonyme : `…/download/` exige un
> compte connecté. Récupération manuelle ou via l'API authentifiée.

## Ambiances (B2) — destination `assets/diapason/ambience/`

| Emplacement | Son | Page | Auteur | Durée | Destination |
|---|---|---|---|---|---|
| `home` | Room Tone Office 13 | <https://freesound.org/s/203306/> | mzui | 2:00 | `ambience/home.ogg` |
| `map` | House Room Tone | <https://freesound.org/s/641306/> | aleclubin | 2:04 | `ambience/map.ogg` |
| `player` | Room Tone Ambience, Low Hum | <https://freesound.org/s/144046/> | gchase | 2:14 | `ambience/player.ogg` |
| `journal` | Iso Booth Ambience | <https://freesound.org/s/127658/> | sacco12 | 0:23 | `ambience/journal.ogg` |
| `familiar` | Roomtone, Server Room, Hum | <https://freesound.org/s/776267/> | soundandmelodies | 1:24 | `ambience/familiar.ogg` |
| `shop` | Coffee shop Ambience | <https://freesound.org/s/540299/> | aidansamuel | 2:17 | `ambience/shop.ogg` |

Complément machine (à superposer sous `familiar`), **téléchargement direct CC0** :
Electronic device loop, qubodup, OpenGameArt —
<https://opengameart.org/sites/default/files/qubodup-edev.flac> (FLAC, 357 Ko).

Seuls `journal` et `shop` sont déclarés bouclables ; les autres sont des room
tones à raccorder par fondu croisé au montage.

## Musiques (B3, B4) — destination `assets/diapason/music/`

| Usage | Son | Page | Auteur | Durée | Destination |
|---|---|---|---|---|---|
| `home` | Calm Ambient Piano Loop | <https://freesound.org/s/832628/> | Jadis0x | 1:26 | `music/home.ogg` |
| `map` | SubsonicAtmosOne | <https://freesound.org/s/223318/> | Diboz | 1:58 | `music/map.ogg` |
| `journal` / `familiar` | Minimalist vibraphone ambience | <https://freesound.org/s/706092/> | xkeril | 2:35 | `music/reflective.ogg` |
| tension | Postapocalyptic Drone | <https://freesound.org/s/452999/> | Breviceps | 0:21 | `music/tension.ogg` |
| game over | Piano Game Over Theme (85 BPM, Fa min.) | <https://freesound.org/s/731657/> | kanaizo | 0:11 | `music/game-over.ogg` |

Réserve documentée dans le plan §4.2 : *Overlook* (`freesound.org/s/506495/`)
est CC0 au champ de licence mais demande un crédit dans sa description — **écarté**
pour lever l'ambiguïté. La musique d'ambiance longue « thriller technologique 2026 »
n'a **aucune source CC0 vérifiée** (plan §6.1) : les pistes ci-dessus sont des
placeholders crédibles, pas l'identité sonore finale.

## Sons complémentaires (B5) — destination `assets/diapason/sfx/`

| Besoin | Son | Page | Auteur | Destination |
|---|---|---|---|---|
| Notification | Notification | <https://freesound.org/s/538149/> | Fupicat | `sfx/notification.ogg` |
| Vibreur | Phone notification on vibrate | <https://freesound.org/s/759616/> | Froey_ | `sfx/phone-vibrate.ogg` |
| Page tournée | Turning page (heavy paper) | <https://freesound.org/s/856497/> | xkeril | `sfx/page-turn.ogg` |

Deux packs Kenney CC0 (téléchargement direct, à dézipper puis trier) :

- Digital Audio — <https://kenney.nl/media/pages/assets/digital-audio/216eac4753-1677590265/kenney_digital-audio.zip>
- UI Audio — <https://kenney.nl/media/pages/assets/ui-audio/490d233f68-1677590494/kenney_ui-audio.zip>

## Fonds photographiques d'ambiance de scénario (B7, plan §5)

Les scénarios 1, 5, 7 et 8 disposent d'une source photo CC0 (Wikimedia Commons,
catégorie *Images from Unsplash*, CC0 réelle antérieure à juin 2017). Voir le plan
§3 et §5 pour les chemins exacts. À redimensionner à 1920 px et réencoder en WebP.
Les cinq autres scénarios sans source photo sont déjà couverts par les illustrations
SVG génératives livrées dans ce lot (`illustrations/`).

## Ce que ce kit laisse explicitement en attente

- **Aucune ambiance ni musique n'est encore dans le pack.** Le manifeste déclare
  honnêtement `ambience=absent` et `music=partiel`, et le bloc `media` garde
  `AmbienceUrl` / `MusicUrl` à `null`.
- Le câblage audio et la mise à jour du manifeste se font **après** la pose
  effective des fichiers, jamais avant.
