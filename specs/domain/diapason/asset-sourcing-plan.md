# Plan d'approvisionnement des assets — Diapason

Document de travail. Il recense ce que le pack `diapason-core` contient réellement, ce
qu'il faudrait pour une démonstration complète, et ce qui est sourçable sous une licence
acceptable. **Aucun téléchargement n'a été effectué** : ce document est une liste de
courses, pas un état des lieux du dépôt.

Règle de licence retenue : usage commercial autorisé **sans attribution obligatoire**.
En pratique cela veut dire **CC0-1.0, Unlicense ou domaine public**, et rien d'autre.
CC-BY est refusé. La licence Pixabay et la licence Unsplash sont refusées elles aussi :
elles autorisent l'usage commercial sans attribution, mais ce sont des licences
propriétaires révocables par simple modification des CGU, sans renonciation aux droits
moraux ni aux brevets. Voir « Sources écartées » en fin de document.

Règle de distribution : **tout asset est téléchargé et versionné dans le dépôt.** Aucun
lien direct vers un hébergeur tiers. Cette règle existe parce qu'elle a déjà été violée
une fois (portrait du familier hotlinké, retiré par le commit `3753378`) — et parce
qu'elle l'est encore aujourd'hui, voir « Dettes ouvertes ».

Toutes les licences citées ci-dessous ont été vérifiées en interrogeant la source
elle-même (API Wikimedia `extmetadata`, page Freesound de chaque son, page Kenney).
Les lignes non vérifiées sont signalées comme telles.

---

## 1. Inventaire réel du pack `diapason-core`

Relevé sur disque, pas sur la documentation. 68 fichiers, 688 Ko au total, dont 62
assets déclarés au manifeste.

| Type | Nombre | Format | Source | Couverture |
|---|---|---|---|---|
| `ui` | 26 | SVG | Kenney UI Pack 2.0 | Complète |
| `icon` | 22 | PNG 100×100 | Kenney Game Icons | Complète |
| `sfx` | 12 | OGG Vorbis | Kenney Interface Sounds | Bonne |
| `stinger` | 4 | OGG Vorbis | Kenney Music Jingles | Partielle (0,6–1,1 s) |
| **Fonds** | **0** | — | — | **Absente** |
| **Ambiances** | **0** | — | — | **Absente** |
| **Musiques** | **0** | — | — | **Absente** |
| **Illustrations** | **0** | — | — | **Absente** |
| **Portraits de familier** | **0** | — | — | **Absente** |
| **Icônes de marque / client** | **0** | — | — | **Absente** |

Le manifeste est honnête sur ses trous : son tableau `gaps` déclare déjà `ambience`
absent, `illustrations` absent et `music` partiel. Ce plan confirme ce diagnostic et
l'étend à trois catégories que le manifeste ne mentionne pas encore : les fonds
d'emplacement, les portraits de familier et les icônes de marque.

Autrement dit : **le pack couvre aujourd'hui le chrome de l'interface et rien d'autre.**
Il n'y a pas une seule image de contenu ni une seule seconde d'audio continu.

---

## 2. Besoins pour une démonstration complète

Emplacements applicatifs, d'après `ConfigurationService.CreateDefaultMedia()` :
`home`, `map`, `player`, `journal`, `familiar`, `shop`. Chaque emplacement expose
`BackgroundUrl`, `AmbienceUrl`, `MusicUrl`, `Bpm`, `Loop`. S'y ajoute
`GameOverMediaDefinition` (`MusicUrl`, `VisualUrl`).

| # | Besoin | Quantité | Statut |
|---|---|---|---|
| B1 | Fonds d'emplacement | 6 | Sourçable |
| B2 | Boucles d'ambiance | 6 | Sourçable |
| B3 | Lits musicaux | 3–6 | **Partiel — voir §6** |
| B4 | Musique de game over | 1 | Sourçable |
| B5 | Sons de choix / interaction / récompense | ~16 | Déjà couvert, extension possible |
| B6 | Portraits de familier (3 formes × 4 tons) | 12 | **Sans source — voir §6** |
| B7 | Illustrations de scénario | 10 | **Sans source — voir §6** |
| B8 | Icônes de marque et de client | 2 | **À produire — voir §6** |
| B9 | Icônes d'interface | 48 | Déjà couvert |

---

## 3. B1 — Fonds d'emplacement

Source : **Wikimedia Commons**, catégorie `Images from Unsplash` — photos Unsplash
antérieures à juin 2017, publiées à l'époque sous CC0-1.0 réelle, et ré-hébergées sur
Commons avec le bandeau CC0 sur chaque fiche. C'est le seul gisement CC0 vérifié qui
soit à la fois contemporain, haute résolution et cohérent avec la direction artistique
2026.

Licence vérifiée fichier par fichier via l'API Commons (`extmetadata.LicenseShortName`
= `CC0`) le 19/07/2026. Les poids ci-dessous sont ceux de l'original ; **tous doivent
être redimensionnés à 1920 px de large et réencodés en WebP avant versionnement**
(cible ≈ 200–400 Ko pièce, contre 2–17 Mo en original).

Racine directe : `https://upload.wikimedia.org/wikipedia/commons/`

| Emplacement | Fichier Commons | Chemin direct | Dim. | Poids | Destination |
|---|---|---|---|---|---|
| `home` | `Clean minimalist office (Unsplash).jpg` | `6/60/Clean_minimalist_office_%28Unsplash%29.jpg` | 7806×5304 | 16,5 Mo | `backgrounds/home.webp` |
| `map` | `City Skyline from Above (Unsplash).jpg` | `b/b9/City_Skyline_from_Above_%28Unsplash%29.jpg` | 5160×3440 | 1,9 Mo | `backgrounds/map.webp` |
| `player` | `Code on computer monitor (Unsplash).jpg` | `7/7f/Code_on_computer_monitor_%28Unsplash%29.jpg` | 5760×3840 | 17,2 Mo | `backgrounds/player.webp` |
| `journal` | `Notebooks-cambridge (Unsplash).jpg` | `6/60/Notebooks-cambridge_%28Unsplash%29.jpg` | 4096×4096 | 6,9 Mo | `backgrounds/journal.webp` |
| `shop` | `Online store on a screen (Unsplash).jpg` | `0/05/Online_store_on_a_screen_%28Unsplash%29.jpg` | 3500×1969 | 4,1 Mo | `backgrounds/shop.webp` |
| `familiar` | — | — | — | — | **Sans source, voir §6.4** |

`journal` est carré : recadrage en 16:9 nécessaire.

Alternates vérifiés CC0, utiles pour les illustrations de scénario (§5) ou en
remplacement :

| Fichier | Chemin direct | Dim. | Poids |
|---|---|---|---|
| `Desks in an open office space (Unsplash).jpg` | `…/Desks_in_an_open_office_space_%28Unsplash%29.jpg` | 4000×2667 | 5,8 Mo |
| `Minimalist meeting room (Unsplash).jpg` | `…/Minimalist_meeting_room_%28Unsplash%29.jpg` | 5695×3797 | 7,1 Mo |
| `Stylish meeting room (Unsplash).jpg` | `…/Stylish_meeting_room_%28Unsplash%29.jpg` | 5517×3639 | 2,3 Mo |
| `Wires and cables (Unsplash).jpg` | `…/Wires_and_cables_%28Unsplash%29.jpg` | 5184×3456 | 11,5 Mo |
| `Laptop on a neat desk (Unsplash).jpg` | `…/Laptop_on_a_neat_desk_%28Unsplash%29.jpg` | 5891×3927 | 8,2 Mo |

Les chemins exacts des alternates sont à relire sur leur fiche Commons — seuls les
cinq fonds principaux ont leur chemin de hachage confirmé ici.

---

## 4. B2 à B5 — Audio

### 4.1 Ambiances (B2)

Source : **Freesound**, filtre CC0. Licence relue sur la page de chaque son le
19/07/2026 : les quinze présentent bien la mention `publicdomain/zero`.

Freesound n'a **pas** d'URL de téléchargement anonyme : `…/download/` exige un compte
connecté. Le téléchargement sera donc manuel ou via l'API authentifiée. Les poids sont
ceux du WAV source ; **tous à réencoder en OGG Vorbis** pour rester cohérent avec le
reste du pack (gain attendu ≈ 90 %).

| Emplacement | Son | Page | Auteur | Durée | Poids | Destination |
|---|---|---|---|---|---|---|
| `home` | Room Tone Office 13 | `freesound.org/s/203306/` | mzui | 2:00 | 20,8 Mo | `ambience/home.ogg` |
| `map` | House Room Tone | `freesound.org/s/641306/` | aleclubin | 2:04 | 68,0 Mo | `ambience/map.ogg` |
| `player` | Room Tone Ambience, Low Hum | `freesound.org/s/144046/` | gchase | 2:14 | 36,8 Mo | `ambience/player.ogg` |
| `journal` | Iso Booth Ambience | `freesound.org/s/127658/` | sacco12 | 0:23 | 3,9 Mo | `ambience/journal.ogg` |
| `familiar` | Roomtone, Server Room, Hum | `freesound.org/s/776267/` | soundandmelodies | 1:24 | 11,6 Mo | `ambience/familiar.ogg` |
| `shop` | Coffee shop Ambience | `freesound.org/s/540299/` | aidansamuel | 2:17 | 37,5 Mo | `ambience/shop.ogg` |

Alternate bureau : Office Ambience 01 OWI, `freesound.org/s/327497/`, DavidFrbr,
1:41, 18,5 Mo, CC0 vérifié.

Complément machine : Electronic device loop, **OpenGameArt**,
`opengameart.org/content/electronic-device-loop`, qubodup, CC0 vérifié, FLAC 357 Ko,
téléchargement direct `opengameart.org/sites/default/files/qubodup-edev.flac`
(HTTP 200 confirmé). À superposer sous l'ambiance `familiar`.

Seuls `journal` et `shop` sont déclarés bouclables par leur auteur. Les quatre autres
sont des room tones : le bouclage sans couture demandera un fondu croisé au montage.

### 4.2 Musiques (B3)

| Usage | Son | Page | Auteur | Durée | Poids | Destination |
|---|---|---|---|---|---|---|
| `home` | Calm Ambient Piano Loop | `freesound.org/s/832628/` | Jadis0x | 1:26 | 21,8 Mo | `music/home.ogg` |
| `map` | SubsonicAtmosOne | `freesound.org/s/223318/` | Diboz | 1:58 | 4,2 Mo | `music/map.ogg` |
| `journal` / `familiar` | Minimalist vibraphone ambience | `freesound.org/s/706092/` | xkeril | 2:35 | 42,5 Mo | `music/reflective.ogg` |
| tension | Postapocalyptic Drone | `freesound.org/s/452999/` | Breviceps | 0:21 | 3,5 Mo | `music/tension.ogg` |
| boucle courte | (Ambient Loop) In the Deep | `freesound.org/s/464920/` | plasterbrain | 0:08 | 573 Ko | `music/loop-short.ogg` |

Alternate piano : Calming Piano Loop 60bpm, `freesound.org/s/679738/`,
Seth_Makes_Sounds, 1:49, 27,5 Mo, CC0 vérifié.

**Réserve sur un titre.** *Overlook*, `freesound.org/s/506495/`, SondreDrakensson,
1:18, 3,0 Mo — le champ de licence dit CC0, mais la description demande un crédit.
Juridiquement CC0, donc utilisable ; contradictoire dans l'intention de l'auteur.
Je ne le retiens pas : le bénéfice ne vaut pas l'ambiguïté.

Ce lot **ne couvre pas** le besoin réel. Voir §6.1.

### 4.3 Game over (B4)

Piano Game Over Theme (85 BPM, Fa mineur), `freesound.org/s/731657/`, kanaizo,
0:11, 2,1 Mo, WAV 48 kHz, CC0 vérifié → `music/game-over.ogg`.
Piano sobre, sans emphase — cohérent avec le registre de Diapason, qui traite l'échec
comme une rupture et non comme une punition.

### 4.4 Sons complémentaires (B5)

Le pack couvre déjà l'essentiel de l'interface. Compléments contemporains :

| Besoin | Son | Page | Auteur | Durée | Poids | Destination |
|---|---|---|---|---|---|---|
| Notification | Notification | `freesound.org/s/538149/` | Fupicat | 0:01 | 119 Ko | `sfx/notification.ogg` |
| Vibreur | Phone notification on vibrate | `freesound.org/s/759616/` | Froey_ | 0:01 | 108 Ko | `sfx/phone-vibrate.ogg` |
| Page tournée | Turning page (heavy paper) | `freesound.org/s/856497/` | xkeril | 0:03 | 299 Ko | `sfx/page-turn.ogg` |

Frappe clavier : Keyboard Typing 10, `freesound.org/s/546167/`, grcekh, 2:50, 6,5 Mo —
CC0 **non revérifié** dans cette passe, à confirmer avant téléchargement.

Deux packs Kenney CC0 que le dépôt n'a pas encore, tous deux confirmés
« Creative Commons CC0 » sur leur page, poids relevés par requête HEAD :

| Pack | URL | Contenu | Poids |
|---|---|---|---|
| Digital Audio | `kenney.nl/media/pages/assets/digital-audio/216eac4753-1677590265/kenney_digital-audio.zip` | 60 sons | 990 Ko |
| UI Audio | `kenney.nl/media/pages/assets/ui-audio/490d233f68-1677590494/kenney_ui-audio.zip` | 50 sons | 412 Ko |

C'est là qu'il faut chercher le bip de badge : aucun son de lecteur RFID n'existe sous
filtre CC0 sur Freesound (zéro résultat sur `access card beep door` et
`rfid badge reader beep`). Un bip de confirmation synthétique fera l'affaire en
contexte.

---

## 5. B7 — Illustrations de scénario

Quatre des dix scénarios ont une correspondance photographique CC0 défendable, toutes
prises dans le gisement Commons du §3 :

| Scénario | Fichier | Qualité du rapprochement |
|---|---|---|
| 1. La note de service | `Desks in an open office space` | Correcte |
| 5. La revue automatique | `Code on computer monitor` | Générique, aucune sémantique de blocage |
| 7. La réunion où personne ne doute | `Stylish meeting room` | **Salle vide** — les neuf cadres manquent |
| 8. Le signalement | `Wires and cables` | Ambiance d'infrastructure seulement |

Les six autres — 2, 3, 4, 6, 9, 10 — **n'ont aucune source**. Ce n'est pas un échec de
recherche, c'est une propriété du besoin : ce sont des **états d'écran**, pas des
scènes. Un écran de refus de vérification d'identité, une revue de code bloquée sur un
diff, un tableau de 412 candidatures classées, un sommaire de spécification, une trace
d'erreur avec l'assistant grisé — cela n'existe pas en photographie de stock, sous
aucune licence. Voir §6.3 pour la recommandation.

---

## 6. Ce qui n'a pas de source

Cette section est la plus importante du document. **Le pack sera troué après exécution
de ce plan, et il faut le dire.**

### 6.1 Musique d'ambiance de qualité — partiellement couvert

Le vivier CC0 en musique se concentre sur deux pôles : la boucle de piano solo et le
drone sci-fi sombre. Aucun titre CC0 vérifié ne vise « électronique contemporain,
légèrement tendu, thriller technologique 2026 », qui est le registre de Diapason. Les
pistes du §4.2 sont utilisables comme placeholders crédibles, pas comme identité
sonore.

Options : commander une piste originale ; générer sous licence commerciale claire ;
acheter une licence (Artlist, Epidemic Sound, ou achat unitaire) ; ou assumer une
démonstration sans musique de fond, en ne gardant qu'ambiance et stingers — ce qui est
défendable, la sobriété étant cohérente avec le propos.

### 6.2 Portraits de familier — aucune source

Douze variantes attendues : formes `spark`, `owl`, `fox` × tons `Warm`, `Playful`,
`Direct`, `Mysterious`. **Il n'existe aucune source CC0 pour cette matrice.**

Trois raisons structurelles, et non un manque de recherche : une matrice 3×4 exige la
main d'un seul illustrateur, or assembler douze cliparts CC0 sans lien donnera
exactement l'air de douze cliparts CC0 sans lien ; `spark` n'est pas un objet
représentable dans les bibliothèques de clipart ; et les hiboux et renards CC0
disponibles (Openclipart en compte plus de 300, tous CC0) sont dans un registre
album jeunesse, frontalement incompatible avec un récit sur le préjudice
algorithmique.

**Recommandation : générer les douze en SVG paramétrique.** La forme s'encode en
géométrie (`spark` = éclat radial, `owl` = deux disques et un bec triangulaire,
`fox` = museau et oreilles triangulaires) et le ton en palette et cinétique
(`Warm` = ambre, easing lent ; `Playful` = multi-teintes saturées, ressort ;
`Direct` = monochrome contrasté, sans easing ; `Mysterious` = indigo, alpha basse,
dérive). Un composant, douze sorties, cohérence garantie, poids négligeable, aucun
risque de licence. Ce n'est pas un pis-aller : pour une matrice forme × ton, c'est la
bonne réponse technique.

Le champ `PortraitUrl` de `FamiliarDefinition` est `null` depuis le commit `3753378`.
**Il doit le rester tant qu'un asset réel n'existe pas.** Un placeholder hotlinké est
précisément ce qui a été retiré.

### 6.3 Six illustrations de scénario sur dix — aucune source

Recommandation identique et pour les mêmes raisons : les produire en HTML/SVG dans le
langage visuel de l'application. Une maquette d'écran de refus, un diff avec un verrou
rouge, une table de classement affichant « 412 résultats » — c'est plus lisible, plus
juste, localisable en français, et sans licence à gérer. Une photo du bureau d'un
inconnu ne raconte aucune de ces dix situations.

### 6.4 Fond `familiar` — aucune source

Le seul candidat CC0 trouvé (`Green Matrix rain on a screen`) est en orientation
portrait et convoque un cliché de 1999 dans un univers situé en 2026. À écarter.
Un dégradé abstrait généré, accordé à la palette du manifeste (`azure` `#2f7fa0` →
`ink` `#17344a`), est préférable et coûte quelques lignes de CSS.

### 6.5 Icônes de marque et de client — à produire, pas à sourcer

Aucun emplacement de marque n'existe aujourd'hui dans le modèle de configuration : ni
`logo`, ni `brand`, ni `clientIcon`. Le besoin implique donc **une évolution de schéma**
avant toute question d'asset.

Sur le fond : un logo ne se source pas dans une bibliothèque de clipart. Le diapason
est trivial à dessiner en SVG et c'est l'identité du produit. Quant au logo client de
démonstration, il doit être **manifestement fictif** — glyphe géométrique neutre et
faux nom — pour qu'aucun lecteur ne puisse le prendre pour une vraie entreprise.

---

## 7. Dettes ouvertes constatées en chemin

Deux points relevés pendant l'inventaire, hors périmètre de ce plan mais à traiter.

1. **Un hotlink Unsplash survit dans le dépôt.** `ConfigurationService.cs`, dans
   `IntroSceneDefinition` de la scène d'introduction Diapason :
   `https://images.unsplash.com/photo-1511497584788-876760111969?auto=format&fit=crop&w=1800&q=85`.
   C'est une photo de forêt — donc à la fois le hotlink que le commit `3753378`
   entendait supprimer et l'illustration hors direction artistique. Le commit a
   corrigé le portrait du familier et manqué celle-ci, dans le même fichier et le même
   document par défaut.

2. **Fixture forêt résiduelle côté iOS.** `GenEngine/Resources/forest-choice.json`,
   chargée par `Features/Developer/DeveloperView.swift` et embarquée par quatre entrées
   du `project.pbxproj`. Chemin réservé aux développeurs, vraisemblablement un reliquat
   de l'univers retiré, mais toujours livré dans le bundle.

Aucun autre hébergeur hotlinké (Pexels, Imgur, Cloudinary) dans les deux dépôts, et
aucune autre référence médiévale.

---

## 8. Sources écartées, et pourquoi

| Source | Motif |
|---|---|
| **Pixabay** | Licence propriétaire, pas CC0. Page inaccessible (HTTP 403), termes courants non vérifiables. Écartée par la règle de licence *et* faute de vérification. |
| **Unsplash (site)** | Licence propriétaire vérifiée : usage commercial sans attribution, mais interdiction de revente non modifiée et de constitution d'un service concurrent, aucune renonciation aux droits moraux, révocable par les CGU. Écartée. Le détour par Wikimedia Commons (§3) est légitime : ces fichiers-là étaient sous CC0 réelle au moment de leur publication, et la dédicace CC0 est irrévocable. |
| **incompetech / Kevin MacLeod** | CC-BY, attribution obligatoire. |
| **publicdomainvectors.org** | HTTP 403 sur la page de licence, non vérifiable. |
| **svgrepo.com** | Mélange CC0, CC-BY et MIT selon la collection ; page de licence en HTTP 429. Utilisable au cas par cas, en lisant le badge de chaque icône. |
| **rawpixel.com** | HTTP 403 ; mélange domaine public et contenu premium dans les mêmes résultats, risque élevé d'erreur de sélection. |
| **Smithsonian / Met / Rijksmuseum** | Réellement CC0, mais fonds d'art historique — mauvais siècle pour un univers 2026. |
| **Ensembles d'icônes MIT / Apache** (Bootstrap Icons, Lucide, Material Symbols) | Imposent la conservation d'une mention de copyright, soit une forme d'attribution. Hors règle, même si le risque pratique est faible. |

---

## 9. Récapitulatif de couverture

| Besoin | Couvert | Restant |
|---|---|---|
| B1 Fonds | 5 / 6 | `familiar` à générer |
| B2 Ambiances | 6 / 6 | Bouclage à monter |
| B3 Musiques | ~2 / 6 en qualité réelle | Identité sonore à produire ou acheter |
| B4 Game over | 1 / 1 | — |
| B5 Sons | 12 + 110 (packs Kenney) | — |
| B6 Portraits de familier | **0 / 12** | Génération SVG paramétrique |
| B7 Illustrations | 4 / 10 | 6 à produire en SVG/HTML |
| B8 Icônes de marque | **0 / 2** | Schéma à étendre, puis à dessiner |
| B9 Icônes d'interface | 48 / 48 | — |

En volume : environ 260 Mo de sources à télécharger, pour un pack final estimé à 8–12 Mo
après redimensionnement et réencodage.

Deux catégories sur neuf restent entièrement sans source, et une troisième n'est
couverte qu'en apparence. **Le pack ne sera pas complet à l'issue de ce plan**, et le
tableau `gaps` du manifeste devra être mis à jour pour déclarer les fonds, les portraits
et les icônes de marque, en plus des trois manques déjà reconnus.
