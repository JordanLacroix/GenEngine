# Sauvegarde et restauration chiffrées des bases

Dernière mise à jour : 17 juillet 2026 (HRD-006).

## Objectif et périmètre

Chaque service autonome (`Authoring`, `Play`, `Identity`) possède sa propre base
PostgreSQL. Cette procédure fournit une sauvegarde **chiffrée**, automatisée et
testée, ainsi qu'une restauration **sûre par défaut**, pour les trois bases.

- Les dumps utilisent le format personnalisé de PostgreSQL (`pg_dump -Fc`), ce qui
  permet une restauration sélective et parallèle via `pg_restore`.
- Le chiffrement repose sur [age](https://github.com/FiloSottile/age). Aucun dump
  en clair n'est écrit sur le disque hôte : `pg_dump` s'exécute dans le conteneur
  et le flux est chiffré à la volée sur l'hôte.
- Aucune clé ni phrase secrète n'est stockée dans le dépôt ni passée en argument
  de ligne de commande.

Cette procédure ne remplace pas une stratégie de sauvegarde de production
(rétention, réplication, stockage hors-site, tests de reprise réguliers). Elle
fournit l'outillage et le format ; les politiques restent à définir avant toute
exposition en production.

## Outils et scripts

| Fichier | Rôle |
|---|---|
| [`scripts/backup-databases.sh`](../../scripts/backup-databases.sh) | Sauvegarde chiffrée des trois bases |
| [`scripts/restore-database.sh`](../../scripts/restore-database.sh) | Restauration (dry-run par défaut, base temporaire, ou écrasement explicite) |
| [`scripts/lib/age-crypto.sh`](../../scripts/lib/age-crypto.sh) | Fonctions de chiffrement/déchiffrement age partagées |

Prérequis :

- `docker` (les bases tournent en conteneurs Compose) ;
- `age` (`brew install age` sur macOS, `apt-get install age` sur Debian) ;
- `python3` (déjà requis par l'outillage du dépôt) : uniquement en mode phrase
  secrète, pour piloter l'invite de mot de passe d'age via un pseudo-terminal.

Les sorties chiffrées sont écrites dans `backups/` (à la racine du dépôt),
répertoire **ignoré par Git** (`.gitignore`).

## Gestion des clés — développement vs production

### Développement local — phrase secrète symétrique

Mode le plus simple : une seule variable d'environnement fournit la phrase
secrète. Elle n'apparaît jamais en clair sur la ligne de commande.

```bash
export BACKUP_AGE_PASSPHRASE='une-phrase-secrète-de-dev-robuste'
```

> age ne lit une phrase secrète que depuis un terminal de contrôle, jamais depuis
> un tuyau. Les scripts pilotent donc `age -p` via un pseudo-terminal
> (`scripts/lib/age-crypto.sh`), tout en gardant le texte clair sur un vrai tuyau.

Ne réutilise pas une phrase secrète de production en développement, et
inversement. Ne commite jamais la valeur.

### Production — destinataires age (chiffrement asymétrique)

En production, on chiffre pour un ou plusieurs **destinataires** (clés publiques
age) et on déchiffre avec un **fichier d'identité** (clé privée) conservé hors du
dépôt, dans un gestionnaire de secrets.

Générer une paire de clés :

```bash
age-keygen -o key.txt          # écrit la clé privée ; affiche la clé publique
# Public key: age1qxy...
```

Chiffrer (sauvegarde) :

```bash
export BACKUP_AGE_RECIPIENTS='age1qxy...'          # une ou plusieurs, séparées par des virgules/espaces
# ou
export BACKUP_AGE_RECIPIENTS_FILE=/chemin/recipients.txt
```

Déchiffrer (restauration) :

```bash
export BACKUP_AGE_IDENTITY_FILE=/chemin/sécurisé/key.txt
```

Règles :

- La clé privée (`key.txt` / `BACKUP_AGE_IDENTITY_FILE`) ne quitte jamais le
  gestionnaire de secrets et n'entre jamais dans le dépôt ni dans une image.
- Chiffrer pour plusieurs destinataires permet la rotation et la récupération
  (par exemple une clé d'opérateur et une clé de secours hors-ligne).
- La rotation se fait en re-chiffrant les sauvegardes pour les nouveaux
  destinataires ; les anciennes clés privées restent nécessaires tant que
  d'anciennes sauvegardes doivent être lisibles.

Le mode est choisi automatiquement : `BACKUP_AGE_PASSPHRASE` active le mode phrase
secrète ; sinon `BACKUP_AGE_RECIPIENTS[_FILE]` active le mode destinataires. En
l'absence des deux, les scripts échouent avec un message explicite.

## Sauvegarde

```bash
export BACKUP_AGE_PASSPHRASE='…'      # ou configuration destinataires
scripts/backup-databases.sh
```

Le script produit trois fichiers horodatés (UTC) dans `backups/` :

```text
authoring-db-<UTC>.dump.age
identity-db-<UTC>.dump.age
play-db-<UTC>.dump.age
```

Chaque fichier commence par l'en-tête `age-encryption.org/v1` et contient un dump
`pg_dump -Fc` chiffré. Le script échoue clairement si `age` est absent (avec une
astuce d'installation), si aucune configuration de chiffrement n'est présente, ou
si un dump est vide ou échoue.

Variables d'environnement optionnelles : `BACKUP_DIR` (répertoire de sortie),
`COMPOSE_FILE` (fichier Compose à utiliser).

## Restauration — sûre par défaut

Le script de restauration prend un **nom de service** et un **fichier chiffré** :

```bash
scripts/restore-database.sh <service> <fichier.age> [options]
#   <service> : authoring-db | identity-db | play-db
```

Trois niveaux, du plus sûr au plus destructeur :

1. **Dry-run (par défaut, sans `--target-db`)** : déchiffre puis exécute
   `pg_restore --list`. Affiche le sommaire de l'archive **sans toucher** à aucune
   base.
2. **Base temporaire (`--target-db <nom>`)** : crée une base **séparée** et y
   restaure le dump. La base réelle du service n'est jamais modifiée, ce qui
   permet de comparer les deux. `--drop-target` remplace une base temporaire
   existante.
3. **Écrasement de la base réelle (`--allow-overwrite-source`)** : restaure
   par-dessus la base vivante (`pg_restore --clean --if-exists`). **Destructeur** ;
   refusé sans ce drapeau explicite.

Après une restauration dans une base temporaire, le script affiche le nombre
**exact** de lignes par table, pour vérifier la fidélité de la restauration.

## Procédure de test

Prérequis : stack active et `age` installé.

```bash
docker compose -f compose.yaml -f compose.observability.yaml up -d --wait
command -v age || brew install age
export BACKUP_AGE_PASSPHRASE='REMPLACE-PAR-UNE-PHRASE-DE-DEV-JETABLE'
```

1. **Sauvegarde des trois bases** :

   ```bash
   scripts/backup-databases.sh
   ```

   Attendu : trois fichiers `*.dump.age` dans `backups/`, tailles non nulles.

2. **Vérifier le chiffrement** (en-tête age, contenu illisible en clair) :

   ```bash
   head -c 21 backups/authoring-db-*.dump.age   # -> age-encryption.org/v1
   ```

3. **Vérifier l'exclusion Git** :

   ```bash
   git check-ignore backups/                    # -> backups/
   ```

4. **Dry-run de restauration** (aucune base modifiée) :

   ```bash
   scripts/restore-database.sh authoring-db backups/authoring-db-*.dump.age
   ```

   Attendu : sommaire `pg_restore --list` (tables, contraintes, index).

5. **Restauration réelle dans une base temporaire** puis comparaison :

   ```bash
   # Comptes de la base source vivante
   docker compose -f compose.yaml exec -T authoring-db \
     psql -U authoring -d genengine_authoring \
     -c "SELECT count(*) FROM scenarios;"

   # Restauration dans une copie jetable
   scripts/restore-database.sh authoring-db backups/authoring-db-*.dump.age \
     --target-db genengine_authoring_restore_check
   ```

   Attendu : les comptes par table affichés par le script correspondent à la
   source. Nettoyer ensuite :

   ```bash
   docker compose -f compose.yaml exec -T authoring-db \
     dropdb -U authoring genengine_authoring_restore_check
   ```

6. **Contrôles de sûreté** (doivent échouer proprement, sans écrire de données) :

   ```bash
   # Refus d'écraser la base réelle sans le drapeau explicite (sortie 3)
   scripts/restore-database.sh authoring-db backups/authoring-db-*.dump.age \
     --target-db genengine_authoring

   # Mauvaise phrase secrète (sortie non nulle, aucune restauration)
   BACKUP_AGE_PASSPHRASE='phrase-incorrecte' \
     scripts/restore-database.sh authoring-db backups/authoring-db-*.dump.age
   ```

## Vérification effectuée pour HRD-006

Sur la stack locale en cours d'exécution (`genengine`) :

- `scripts/backup-databases.sh` a produit trois sauvegardes chiffrées
  (`authoring-db` 7301 o, `identity-db` 3976 o, `play-db` 7280 o), chacune avec
  l'en-tête `age-encryption.org/v1`.
- `backups/` est bien ignoré par Git (`git check-ignore`).
- Dry-run `pg_restore --list` sur `authoring-db` : sommaire lisible (format
  `CUSTOM`, 17 entrées TOC).
- Restauration réelle de `authoring-db` dans
  `genengine_authoring_restore_check` : `scenarios = 5` et
  `scenario_versions = 5`, identiques à la base source vivante ; la base réelle
  n'a pas été modifiée ; la copie temporaire a été supprimée.
- Contrôles de sûreté : refus d'écrasement de la source sans
  `--allow-overwrite-source` (sortie 3) et échec propre sur mauvaise phrase
  secrète (sortie non nulle).

## Points d'attention

- Les identifiants PostgreSQL présents dans Compose sont des valeurs de
  développement local uniquement.
- Le mode phrase secrète nécessite `python3` pour piloter l'invite d'age ; le mode
  destinataires n'en a pas besoin.
- `n_live_tup` peut être une estimation ; le script utilise `count(*)` pour un
  compte exact.
- À définir avant la production : rétention, chiffrement pour destinataires de
  secours, stockage hors-site et test de reprise périodique (via un ADR).
