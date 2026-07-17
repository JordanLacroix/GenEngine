#!/usr/bin/env bash
#
# restore-database.sh -- decrypt an age-encrypted GenEngine backup and restore
# it, safely by default.
#
# The encrypted dump is decrypted on the host and streamed into `pg_restore`
# running inside the target postgres container. Nothing is written to the host
# disk in plaintext.
#
# SAFETY MODEL
#   * With no --target-db, the script performs a DRY RUN: it decrypts the backup
#     and runs `pg_restore --list`, showing the table of contents WITHOUT
#     touching any database.
#   * --target-db <name> restores into a *separate* database (created fresh),
#     so the live database is never modified and can be compared against it.
#   * Restoring on top of the service's real database is destructive and must be
#     requested explicitly with --allow-overwrite-source.
#
# Usage:
#   scripts/restore-database.sh <service> <backup-file.age> [options]
#
#   <service>   one of: authoring-db | identity-db | play-db
#
# Options:
#   --target-db <name>          restore into this (temporary) database
#   --drop-target               drop <name> first if it already exists
#   --allow-overwrite-source    restore on top of the live database (DESTRUCTIVE)
#   -h, --help                  show this help
#
# Decryption mode is chosen from the environment (see lib/age-crypto.sh):
#   BACKUP_AGE_PASSPHRASE      symmetric passphrase (local dev)
#   BACKUP_AGE_IDENTITY_FILE   age identity / private key (production)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
REPO_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
# shellcheck source=scripts/lib/age-crypto.sh
. "$SCRIPT_DIR/lib/age-crypto.sh"

COMPOSE_FILE="${COMPOSE_FILE:-$REPO_ROOT/compose.yaml}"

usage() { sed -n '2,40p' "${BASH_SOURCE[0]}" | sed 's/^# \{0,1\}//'; }

# service -> database:user
service_meta() {
  case "$1" in
    authoring-db) echo "genengine_authoring:authoring" ;;
    identity-db)  echo "genengine_identity:identity" ;;
    play-db)      echo "genengine_play:play" ;;
    *) return 1 ;;
  esac
}

SERVICE="" BACKUP_FILE="" TARGET_DB="" DROP_TARGET=0 OVERWRITE_SOURCE=0
POSITIONAL=()
while [ $# -gt 0 ]; do
  case "$1" in
    --target-db) TARGET_DB="${2:?--target-db requires a name}"; shift 2 ;;
    --drop-target) DROP_TARGET=1; shift ;;
    --allow-overwrite-source) OVERWRITE_SOURCE=1; shift ;;
    -h|--help) usage; exit 0 ;;
    -*) echo "error: unknown option '$1'" >&2; exit 2 ;;
    *) POSITIONAL+=("$1"); shift ;;
  esac
done

if [ "${#POSITIONAL[@]}" -ne 2 ]; then
  echo "error: expected <service> and <backup-file>." >&2
  echo >&2
  usage >&2
  exit 2
fi
SERVICE="${POSITIONAL[0]}"
BACKUP_FILE="${POSITIONAL[1]}"

meta="$(service_meta "$SERVICE")" || { echo "error: unknown service '$SERVICE' (expected authoring-db|identity-db|play-db)." >&2; exit 2; }
IFS=':' read -r SOURCE_DB DB_USER <<<"$meta"

[ -f "$BACKUP_FILE" ] || { echo "error: backup file not found: $BACKUP_FILE" >&2; exit 2; }

age_require_tool
command -v docker >/dev/null 2>&1 || { echo "error: docker not found on PATH." >&2; exit 127; }

compose() { docker compose -f "$COMPOSE_FILE" "$@"; }
# Run psql (non-interactive, unaligned tuples-only) against a database in the container.
psql_q() { compose exec -T "$SERVICE" psql -U "$DB_USER" -d "$1" -tAc "$2"; }

# ---------------------------------------------------------------------------
# Dry run (default): list the archive contents, touch nothing.
# ---------------------------------------------------------------------------
if [ -z "$TARGET_DB" ]; then
  echo "DRY RUN -- decrypt '$BACKUP_FILE' and list contents (no database is modified)."
  echo "Pass --target-db <name> to restore into a temporary database."
  echo
  age_decrypt "$BACKUP_FILE" - | compose exec -T "$SERVICE" pg_restore --list
  echo
  echo "Dry run complete. No data was written."
  exit 0
fi

# ---------------------------------------------------------------------------
# Real restore into an explicit target database.
# ---------------------------------------------------------------------------
if [ "$TARGET_DB" = "$SOURCE_DB" ]; then
  if [ "$OVERWRITE_SOURCE" -ne 1 ]; then
    cat >&2 <<EOF
error: refusing to restore on top of the live database '$SOURCE_DB'.
This is destructive. Re-run with --allow-overwrite-source if you really mean it,
or choose a different --target-db to restore into a throwaway copy.
EOF
    exit 3
  fi
  echo "WARNING: restoring on top of the LIVE database '$SOURCE_DB' (--allow-overwrite-source)."
  echo "-> decrypt + pg_restore --clean --if-exists into $SOURCE_DB"
  age_decrypt "$BACKUP_FILE" - \
    | compose exec -T "$SERVICE" pg_restore -U "$DB_USER" -d "$SOURCE_DB" --clean --if-exists --no-owner
  echo "Restore into live database '$SOURCE_DB' complete."
  exit 0
fi

# Temporary / separate target database.
exists="$(psql_q "$SOURCE_DB" "SELECT 1 FROM pg_database WHERE datname='$TARGET_DB'")"
if [ "$exists" = "1" ]; then
  if [ "$DROP_TARGET" -ne 1 ]; then
    echo "error: target database '$TARGET_DB' already exists. Pass --drop-target to replace it." >&2
    exit 3
  fi
  echo "-> dropping existing target database '$TARGET_DB'"
  compose exec -T "$SERVICE" dropdb -U "$DB_USER" "$TARGET_DB"
fi

echo "Restore into temporary database (live '$SOURCE_DB' is left untouched)."
echo "-> creating database '$TARGET_DB'"
compose exec -T "$SERVICE" createdb -U "$DB_USER" "$TARGET_DB"

echo "-> decrypt + pg_restore into '$TARGET_DB'"
age_decrypt "$BACKUP_FILE" - \
  | compose exec -T "$SERVICE" pg_restore -U "$DB_USER" -d "$TARGET_DB" --no-owner

echo
echo "Restore complete. Verification (exact row counts per table in '$TARGET_DB'):"
compose exec -T "$SERVICE" psql -U "$DB_USER" -d "$TARGET_DB" <<'SQL'
SELECT format('SELECT %L AS table, count(*) AS rows FROM %I.%I', relname, schemaname, relname)
FROM pg_stat_user_tables
ORDER BY schemaname, relname
\gexec
SQL

echo
echo "Compare against the live source database '$SOURCE_DB' with, e.g.:"
echo "  docker compose -f $COMPOSE_FILE exec -T $SERVICE \\"
echo "    psql -U $DB_USER -d $SOURCE_DB -c 'SELECT count(*) FROM <table>;'"
echo "Drop the temporary copy when done:"
echo "  docker compose -f $COMPOSE_FILE exec -T $SERVICE dropdb -U $DB_USER $TARGET_DB"
