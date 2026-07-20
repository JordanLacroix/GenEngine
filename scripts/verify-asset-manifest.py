#!/usr/bin/env python3
"""Verifie l'integrite d'un manifeste d'assets GenEngine.

Controles effectues :
  1. le manifeste est un JSON valide et expose les champs attendus ;
  2. chaque chemin declare resout vers un fichier existant ;
  3. la taille et l'empreinte SHA-256 declarees correspondent au fichier ;
  4. chaque fichier s'ouvre reellement comme le type qu'il declare
     (signature binaire PNG/Ogg/MP4/ADTS/MP3, element racine SVG, en-tete Vorbis) ;
  5. les dimensions et durees declarees correspondent aux en-tetes decodes ;
     ATTENTION : la duree n'est recalculee que pour l'Ogg Vorbis. Pour l'AAC
     (audio/mp4, audio/aac) et le MP3, seuls le conteneur et la presence des
     metadonnees declarees sont verifies : ces formats exigent un parcours de
     boites MP4 ou de trames a debit variable qu'un script sans dependance ne
     peut faire honnetement. La duree y est declarative, pas prouvee ;
  6. chaque `sourceId` reference existe et pointe vers un fichier de licence present ;
  7. aucun fichier livre n'est absent du manifeste.

Usage : python3 scripts/verify-asset-manifest.py [chemin/vers/asset-manifest.json]
Sortie : 0 si tout est conforme, 1 sinon.
"""
import hashlib
import json
import os
import re
import struct
import sys

DEFAULT_MANIFEST = os.path.join("assets", "diapason", "asset-manifest.json")
IGNORED_SUFFIXES = ("asset-manifest.json", "LICENSES.md")
IGNORED_DIRS = ("licenses",)

errors = []
checks = 0


def fail(message):
    errors.append(message)


def check(condition, message):
    global checks
    checks += 1
    if not condition:
        fail(message)
    return condition


def sha256_of(path):
    digest = hashlib.sha256()
    with open(path, "rb") as handle:
        for chunk in iter(lambda: handle.read(65536), b""):
            digest.update(chunk)
    return digest.hexdigest()


def decode_png(path):
    """Retourne (largeur, hauteur) en verifiant la signature et le chunk IHDR."""
    with open(path, "rb") as handle:
        header = handle.read(33)
    if header[:8] != b"\x89PNG\r\n\x1a\n":
        raise ValueError("signature PNG absente")
    if header[12:16] != b"IHDR":
        raise ValueError("chunk IHDR absent")
    width, height = struct.unpack(">II", header[16:24])
    return width, height


def decode_svg(path):
    """Retourne (largeur, hauteur) en verifiant l'element racine SVG."""
    with open(path, "r", encoding="utf-8") as handle:
        head = handle.read(1024)
    if "<svg" not in head:
        raise ValueError("element racine <svg> absent")
    if "http://www.w3.org/2000/svg" not in head:
        raise ValueError("espace de noms SVG absent")
    width = re.search(r'width="([0-9.]+)"', head)
    height = re.search(r'height="([0-9.]+)"', head)
    if not width or not height:
        raise ValueError("attributs width/height absents")
    return int(float(width.group(1))), int(float(height.group(1)))


def decode_ogg(path):
    """Retourne (canaux, frequence, duree) depuis les en-tetes Ogg/Vorbis.

    La duree est calculee a partir du `granule position` de la derniere page
    Ogg, divise par la frequence d'echantillonnage declaree dans l'en-tete
    d'identification Vorbis.
    """
    with open(path, "rb") as handle:
        data = handle.read()
    if data[:4] != b"OggS":
        raise ValueError("signature Ogg absente")
    marker = data.find(b"\x01vorbis")
    if marker < 0:
        raise ValueError("en-tete d'identification Vorbis absent")
    channels = data[marker + 11]
    sample_rate = struct.unpack("<I", data[marker + 12:marker + 16])[0]
    if sample_rate <= 0:
        raise ValueError("frequence d'echantillonnage invalide")
    last = data.rfind(b"OggS")
    if last < 0:
        raise ValueError("derniere page Ogg introuvable")
    granule = struct.unpack("<q", data[last + 6:last + 14])[0]
    return channels, sample_rate, granule / float(sample_rate)


def decode_mp4(path):
    """Verifie qu'un fichier .m4a est bien un conteneur MP4 audio.

    Controle la boite `ftyp` en tete et la presence d'une boite `moov`. La
    duree n'est pas recalculee : la lire imposerait de descendre la hierarchie
    de boites jusqu'a `mvhd` et de gerer les variantes 32/64 bits, ce que ce
    script sans dependance ne fait pas semblant de savoir faire.
    """
    with open(path, "rb") as handle:
        data = handle.read()
    if len(data) < 12 or data[4:8] != b"ftyp":
        raise ValueError("boite ftyp absente")
    brand = data[8:12]
    if brand[:3] not in (b"M4A", b"M4B", b"mp4", b"iso"):
        raise ValueError("marque de conteneur inattendue (%r)" % brand)
    if b"moov" not in data:
        raise ValueError("boite moov absente")


def decode_adts(path):
    """Verifie la synchronisation ADTS d'un flux AAC brut."""
    with open(path, "rb") as handle:
        head = handle.read(4)
    if len(head) < 4 or head[0] != 0xFF or (head[1] & 0xF6) != 0xF0:
        raise ValueError("synchronisation ADTS absente")


def decode_mp3(path):
    """Verifie l'en-tete ID3v2 ou la synchronisation d'une trame MPEG audio."""
    with open(path, "rb") as handle:
        head = handle.read(3)
    if head[:3] == b"ID3":
        return
    if len(head) < 2 or head[0] != 0xFF or (head[1] & 0xE0) != 0xE0:
        raise ValueError("ni en-tete ID3 ni synchronisation de trame MPEG")


def check_declared_audio(asset_id, asset, media):
    """Exige les metadonnees audio quand le format n'est pas re-decode."""
    audio = asset.get("audio", {})
    for field in ("codec", "channels", "sampleRate", "durationSeconds"):
        check(audio.get(field) is not None,
              "%s : champ audio.%s manquant, obligatoire pour %s (non recalculable ici)" % (asset_id, field, media))


def main():
    manifest_path = sys.argv[1] if len(sys.argv) > 1 else DEFAULT_MANIFEST
    if not os.path.isfile(manifest_path):
        print("manifeste introuvable : %s" % manifest_path)
        return 1
    with open(manifest_path, "r", encoding="utf-8") as handle:
        try:
            manifest = json.load(handle)
        except ValueError as exc:
            print("JSON invalide : %s" % exc)
            return 1
    print("manifeste : %s (JSON valide)" % manifest_path)

    pack_root = os.path.dirname(os.path.abspath(manifest_path))
    sources = {s["sourceId"]: s for s in manifest.get("sources", [])}
    check(bool(sources), "aucune source declaree")

    for source in manifest.get("sources", []):
        for field in ("name", "author", "pageUrl", "downloadUrl", "license", "licenseUrl", "attribution", "licenseFile"):
            check(source.get(field), "source %s : champ %s manquant" % (source.get("sourceId"), field))
        license_path = os.path.join(pack_root, source.get("licenseFile", ""))
        check(os.path.isfile(license_path), "source %s : fichier de licence absent (%s)" % (source.get("sourceId"), source.get("licenseFile")))
        check(source.get("license") == "CC0-1.0", "source %s : licence non CC0-1.0" % source.get("sourceId"))

    seen_ids = set()
    referenced = set()
    assets = manifest.get("assets", [])
    check(bool(assets), "aucun asset declare")

    for asset in assets:
        asset_id = asset.get("id", "<sans id>")
        check(asset_id not in seen_ids, "identifiant duplique : %s" % asset_id)
        seen_ids.add(asset_id)
        check(asset.get("sourceId") in sources, "%s : sourceId inconnu (%s)" % (asset_id, asset.get("sourceId")))

        rel = asset.get("path", "")
        check(bool(rel) and not os.path.isabs(rel) and ".." not in rel, "%s : chemin absent ou non relatif" % asset_id)
        full = os.path.join(pack_root, rel)
        if not check(os.path.isfile(full), "%s : fichier absent (%s)" % (asset_id, rel)):
            continue
        referenced.add(os.path.normpath(rel))

        check(os.path.getsize(full) == asset.get("bytes"), "%s : taille declaree incorrecte" % asset_id)
        check(sha256_of(full) == asset.get("sha256"), "%s : empreinte SHA-256 incorrecte" % asset_id)

        media = asset.get("mediaType")
        try:
            if media == "image/png":
                width, height = decode_png(full)
                check((width, height) == (asset["image"]["width"], asset["image"]["height"]),
                      "%s : dimensions PNG declarees %sx%s, decodees %sx%s" % (
                          asset_id, asset["image"]["width"], asset["image"]["height"], width, height))
            elif media == "image/svg+xml":
                width, height = decode_svg(full)
                check((width, height) == (asset["image"]["width"], asset["image"]["height"]),
                      "%s : dimensions SVG declarees %sx%s, decodees %sx%s" % (
                          asset_id, asset["image"]["width"], asset["image"]["height"], width, height))
            elif media == "audio/ogg":
                channels, rate, duration = decode_ogg(full)
                audio = asset.get("audio", {})
                check(channels == audio.get("channels"), "%s : canaux declares %s, decodes %s" % (asset_id, audio.get("channels"), channels))
                check(rate == audio.get("sampleRate"), "%s : frequence declaree %s, decodee %s" % (asset_id, audio.get("sampleRate"), rate))
                check(abs(duration - float(audio.get("durationSeconds", -1))) < 0.05,
                      "%s : duree declaree %ss, decodee %.3fs" % (asset_id, audio.get("durationSeconds"), duration))
            elif media == "audio/mp4":
                decode_mp4(full)
                check_declared_audio(asset_id, asset, media)
            elif media == "audio/aac":
                decode_adts(full)
                check_declared_audio(asset_id, asset, media)
            elif media == "audio/mpeg":
                decode_mp3(full)
                check_declared_audio(asset_id, asset, media)
            else:
                fail("%s : mediaType non supporte (%s)" % (asset_id, media))
        except (ValueError, KeyError, IndexError, struct.error) as exc:
            fail("%s : le fichier ne se decode pas comme %s (%s)" % (asset_id, media, exc))

    on_disk = set()
    for dirpath, dirnames, filenames in os.walk(pack_root):
        dirnames[:] = [d for d in dirnames if d not in IGNORED_DIRS]
        for name in filenames:
            rel = os.path.relpath(os.path.join(dirpath, name), pack_root)
            if rel.endswith(IGNORED_SUFFIXES) or name.startswith("."):
                continue
            on_disk.add(os.path.normpath(rel))
    for orphan in sorted(on_disk - referenced):
        fail("fichier livre mais absent du manifeste : %s" % orphan)

    print("assets declares : %d" % len(assets))
    print("fichiers presents et references : %d" % len(referenced))
    print("controles executes : %d" % checks)
    if errors:
        print("\nECHEC : %d probleme(s)" % len(errors))
        for message in errors:
            print("  - %s" % message)
        return 1
    print("\nSUCCES : manifeste et fichiers coherents")
    return 0


if __name__ == "__main__":
    sys.exit(main())
